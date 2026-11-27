//! Wire protocol DTOs exchanged with `mkv-rename-cli serve --stdio`.

use std::fmt;

use serde::{Deserialize, Serialize};

/// Expected NDJSON protocol version for the `hello` handshake.
pub const EXPECTED_PROTOCOL_VERSION: u32 = 1;

/// Outgoing JSON-RPC request envelope.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RpcRequest<'a, P: Serialize> {
    pub id: String,
    pub method: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub params: Option<P>,
}

/// Structured error payload returned inside a failed `RpcResponse`.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RpcErrorPayload {
    pub code: String,
    pub message: String,
    pub detail: Option<String>,
}

/// Incoming JSON-RPC response envelope.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RpcResponse<T> {
    #[expect(dead_code, reason = "deserialized from RPC response envelope")]
    pub id: Option<String>,
    pub ok: bool,
    pub result: Option<T>,
    pub error: Option<RpcErrorPayload>,
}

/// Handshake payload returned by the `hello` RPC method.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HelloPayload {
    pub protocol_version: u32,
    #[expect(dead_code, reason = "deserialized from hello handshake payload")]
    pub app: String,
    pub tokens: Vec<PatternToken>,
}

/// `TVMaze` show summary returned by `search`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Show {
    pub id: i64,
    #[expect(dead_code, reason = "deserialized from TVMaze show payload")]
    pub score: f64,
    pub name: String,
    pub year_range: String,
    pub air_network_label: String,
    #[expect(dead_code, reason = "deserialized from TVMaze show payload")]
    pub plain_summary: String,
}

/// `TVMaze` season summary returned by `seasons`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Season {
    pub id: i64,
    pub name: String,
    pub number: i32,
    pub total_episode_count: i32,
}

/// `TVMaze` episode metadata returned by `episodes`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
#[expect(
    clippy::struct_field_names,
    reason = "matches mkv-rename-cli JSON wire field name"
)]
pub struct Episode {
    #[expect(dead_code, reason = "deserialized from TVMaze episode payload")]
    pub id: i64,
    pub name: String,
    pub season: i32,
    pub episode_number: Option<i32>,
    pub run_time: i64,
    pub air_date: Option<String>,
}

/// Discovered MKV media file returned by `scan`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MkvFile {
    #[expect(dead_code, reason = "deserialized from scan payload")]
    pub root: String,
    pub full_path: String,
    #[expect(dead_code, reason = "deserialized from scan payload")]
    pub size_in_bytes: Option<i64>,
    #[expect(dead_code, reason = "deserialized from scan payload")]
    pub is_included: bool,
}

/// Non-fatal file import warning returned by `scan`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
#[expect(dead_code, reason = "deserialized from scan payload for issue counts")]
#[expect(
    clippy::struct_field_names,
    reason = "matches mkv-rename-cli JSON wire field name"
)]
pub struct FileImportIssue {
    pub display_name: String,
    pub reason: String,
    pub file_import_issue_type: String,
}

/// Result of scanning one or more paths for MKV files.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FileImportResult {
    pub imported_files: Vec<MkvFile>,
    pub file_import_issues: Vec<FileImportIssue>,
}

/// Filename template token metadata returned by `hello` or `tokens`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PatternToken {
    pub name: String,
    pub description: String,
    pub example_value: String,
}

/// Pattern syntax validation error returned inside `RenamePlan`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PatternError {
    pub column: i32,
    pub token_name: String,
    pub suggestion: Option<String>,
}

/// Output filename casing transformation.
#[derive(Debug, Default, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum CaseStyle {
    #[default]
    Default,
    SnakeCase,
    PascalCase,
    CamelCase,
}

impl CaseStyle {
    /// Returns the next case style in the UI cycle order.
    #[must_use]
    pub const fn next(self) -> Self {
        match self {
            Self::Default => Self::SnakeCase,
            Self::SnakeCase => Self::PascalCase,
            Self::PascalCase => Self::CamelCase,
            Self::CamelCase => Self::Default,
        }
    }

    /// Returns the human-readable display label for this case style.
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Default => "Default",
            Self::SnakeCase => "snake_case",
            Self::PascalCase => "PascalCase",
            Self::CamelCase => "camelCase",
        }
    }
}

impl fmt::Display for CaseStyle {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.label())
    }
}

/// Paired source file and episode metadata sent to `plan`.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenameCandidate {
    pub source_path: String,
    pub season: i32,
    pub episode_number: Option<i32>,
    pub episode_name: String,
    pub run_time: i64,
    pub air_date: Option<String>,
}

/// Request payload for building a rename preview plan.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenamePlanRequest {
    pub show_name: String,
    pub pattern: String,
    pub prefix: String,
    pub case_style: CaseStyle,
    pub target_directory: Option<String>,
    pub candidates: Vec<RenameCandidate>,
}

/// Single planned file rename item.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RenamePlanItem {
    pub operation_id: i32,
    pub source_path: String,
    pub source_file_name: String,
    pub target_path: Option<String>,
    pub target_file_name: Option<String>,
    pub status: String,
}

/// Complete dry-run rename plan returned by `plan`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RenamePlan {
    pub is_pattern_valid: bool,
    pub ready_count: i32,
    pub conflict_count: i32,
    pub skipped_count: i32,
    pub done_count: i32,
    pub errors: Vec<PatternError>,
    pub items: Vec<RenamePlanItem>,
}

/// Single concrete file rename operation sent to `rename`.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenameFileOperation {
    pub operation_id: i32,
    pub source_path: String,
    pub target_path: Option<String>,
}

/// Outcome of executing or undoing one rename operation.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RenameOperationResult {
    pub operation_id: i32,
    pub source_path: String,
    pub target_path: Option<String>,
    pub is_successful: bool,
    pub error_message: Option<String>,
}
