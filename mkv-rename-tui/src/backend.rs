//! Process supervision and NDJSON RPC client for `mkv-rename-cli`.

use std::io::{BufRead, BufReader, BufWriter, Write};
use std::path::PathBuf;
use std::process::{Child, ChildStdin, ChildStdout, Command, Stdio};

use serde::Serialize;
use serde::de::DeserializeOwned;

use crate::error::TuiError;
use crate::protocol::{
    EXPECTED_PROTOCOL_VERSION, Episode, FileImportResult, HelloPayload, PatternToken,
    RenameFileOperation, RenameOperationResult, RenamePlan, RenamePlanRequest, RpcRequest,
    RpcResponse, Season, Show,
};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct SearchParams<'a> {
    query: &'a str,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ShowIdParams {
    show_id: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct SeasonIdParams {
    season_id: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ScanParams<'a> {
    paths: &'a [String],
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct RenameExecuteParams<'a> {
    operations: &'a [RenameFileOperation],
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct UndoRenameParams<'a> {
    results: &'a [RenameOperationResult],
}

/// Initial capacity (4 KiB) for the reusable NDJSON response buffer.
///
/// Large enough to hold typical `hello`, `search`, and `plan` responses without
/// repeated reallocation while keeping per-client memory usage small.
const INITIAL_RESPONSE_BUF_CAPACITY: usize = 4096;

/// Owns the long-lived `mkv-rename-cli serve --stdio` child process.
///
/// Implements `Drop` so the child process is always cleanly terminated on exit or panic.
#[derive(Debug)]
pub struct BackendClient {
    child: Child,
    stdin: Option<BufWriter<ChildStdin>>,
    stdout: BufReader<ChildStdout>,
    response_buf: String,
    next_id: u64,
}

impl BackendClient {
    /// Spawns `mkv-rename-cli serve --stdio` and verifies the `hello` handshake.
    ///
    /// # Errors
    /// Returns [`TuiError`] if the backend fails to spawn, closes stdout, or reports an
    /// incompatible protocol version.
    pub fn spawn() -> Result<(Self, Vec<PatternToken>), TuiError> {
        let mut command = resolve_backend_executable();
        command.args(["serve", "--stdio"]);
        let mut child = command
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::null())
            .spawn()?;

        let stdin = child.stdin.take().ok_or(TuiError::BackendClosed)?;
        let stdout = child.stdout.take().ok_or(TuiError::BackendClosed)?;

        let mut client = Self {
            child,
            stdin: Some(BufWriter::new(stdin)),
            stdout: BufReader::new(stdout),
            response_buf: String::with_capacity(INITIAL_RESPONSE_BUF_CAPACITY),
            next_id: 1,
        };

        let hello = client.hello()?;
        if hello.protocol_version != EXPECTED_PROTOCOL_VERSION {
            return Err(TuiError::ProtocolMismatch {
                expected: EXPECTED_PROTOCOL_VERSION,
                actual: hello.protocol_version,
            });
        }

        Ok((client, hello.tokens))
    }

    /// Performs the initial `hello` protocol handshake.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn hello(&mut self) -> Result<HelloPayload, TuiError> {
        self.call::<(), HelloPayload>("hello", None)
    }

    /// Searches `TVMaze` for shows matching `query`.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn search_shows(&mut self, query: &str) -> Result<Vec<Show>, TuiError> {
        self.call("search", Some(SearchParams { query }))
    }

    /// Retrieves seasons for `show_id`.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn get_seasons(&mut self, show_id: i64) -> Result<Vec<Season>, TuiError> {
        self.call("seasons", Some(ShowIdParams { show_id }))
    }

    /// Retrieves episodes for `season_id`.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn get_episodes(&mut self, season_id: i64) -> Result<Vec<Episode>, TuiError> {
        self.call("episodes", Some(SeasonIdParams { season_id }))
    }

    /// Scans `paths` for valid MKV media files.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn scan_paths(&mut self, paths: &[String]) -> Result<FileImportResult, TuiError> {
        self.call("scan", Some(ScanParams { paths }))
    }

    /// Computes a dry-run rename plan for `request`.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn build_plan(&mut self, request: &RenamePlanRequest) -> Result<RenamePlan, TuiError> {
        self.call("plan", Some(request))
    }

    /// Executes the planned rename `operations` on disk.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn execute_rename(
        &mut self,
        operations: &[RenameFileOperation],
    ) -> Result<Vec<RenameOperationResult>, TuiError> {
        self.call("rename", Some(RenameExecuteParams { operations }))
    }

    /// Reverses previously applied rename `results`.
    ///
    /// # Errors
    /// Returns [`TuiError`] on I/O, JSON, or backend RPC failure.
    pub fn undo_rename(
        &mut self,
        results: &[RenameOperationResult],
    ) -> Result<Vec<RenameOperationResult>, TuiError> {
        self.call("undo", Some(UndoRenameParams { results }))
    }

    fn call<P: Serialize, R: DeserializeOwned>(
        &mut self,
        method: &str,
        params: Option<P>,
    ) -> Result<R, TuiError> {
        let id = self.next_id.to_string();
        self.next_id += 1;

        let request = RpcRequest { id, method, params };

        let stdin = self.stdin.as_mut().ok_or(TuiError::BackendClosed)?;
        serde_json::to_writer(&mut *stdin, &request)?;
        writeln!(stdin)?;
        stdin.flush()?;

        self.response_buf.clear();
        let bytes_read = self.stdout.read_line(&mut self.response_buf)?;
        if bytes_read == 0 {
            return Err(TuiError::BackendClosed);
        }

        let response: RpcResponse<R> = serde_json::from_str(&self.response_buf)?;
        if response.ok {
            response.result.ok_or(TuiError::BackendClosed)
        } else if let Some(err) = response.error {
            Err(TuiError::BackendRpc {
                code: err.code,
                message: err.message,
                detail: err.detail,
            })
        } else {
            Err(TuiError::BackendClosed)
        }
    }
}

impl Drop for BackendClient {
    fn drop(&mut self) {
        // Closing stdin signals EOF so `serve --stdio` exits gracefully.
        drop(self.stdin.take());
        if let Ok(None) = self.child.try_wait() {
            let _ = self.child.kill();
        }
        let _ = self.child.wait();
    }
}

/// Resolves the `mkv-rename-cli` executable from env, sibling binary, dev build, or `PATH`.
#[must_use]
pub fn resolve_backend_executable() -> Command {
    if let Ok(explicit) = std::env::var("MKV_RENAME_CLI_BIN") {
        return Command::new(explicit);
    }

    if let Ok(exe) = std::env::current_exe()
        && let Some(dir) = exe.parent()
    {
        let sibling = dir.join("mkv-rename-cli");
        if sibling.is_file() {
            return Command::new(sibling);
        }
    }

    // Dev workspace fallback: run the compiled CoreCLR Profile or Release dll via `dotnet`.
    let dev_candidates = [
        PathBuf::from("MkvRenameWizard/MkvRenameWizard.Cli/bin/Profile/net10.0/mkv-rename-cli.dll"),
        PathBuf::from("MkvRenameWizard/MkvRenameWizard.Cli/bin/Release/net10.0/mkv-rename-cli.dll"),
        PathBuf::from("../MkvRenameWizard/MkvRenameWizard.Cli/bin/Profile/net10.0/mkv-rename-cli.dll"),
        PathBuf::from("../MkvRenameWizard/MkvRenameWizard.Cli/bin/Release/net10.0/mkv-rename-cli.dll"),
    ];
    for candidate in dev_candidates {
        if candidate.is_file() {
            let mut cmd = Command::new("dotnet");
            cmd.arg(candidate);
            return cmd;
        }
    }

    Command::new("mkv-rename-cli")
}
