//! Error types for the `mkv-rename-tui` frontend.

use std::fmt;

use thiserror::Error;

/// Formats an optional RPC diagnostic detail as a parenthetical suffix without allocating.
struct DetailSuffix<'a>(Option<&'a str>);

impl fmt::Display for DetailSuffix<'_> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(detail) = self.0.filter(|d| !d.is_empty()) {
            write!(f, " ({detail})")?;
        }
        Ok(())
    }
}

/// Top-level error returned by the TUI runtime and backend client.
#[derive(Debug, Error)]
pub enum TuiError {
    /// Standard I/O failure while spawning or talking to the backend process.
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),

    /// JSON serialization or deserialization failure on the RPC stream.
    #[error("JSON serialization error: {0}")]
    Json(#[from] serde_json::Error),

    /// Backend process exited or closed its stdout pipe unexpectedly.
    #[error("Backend process closed stdout unexpectedly")]
    BackendClosed,

    /// Structured RPC error returned by `mkv-rename-cli serve --stdio`.
    #[error("Backend RPC error ({code}): {message}{}", DetailSuffix(.detail.as_deref()))]
    BackendRpc {
        /// Machine-readable error code.
        code: String,
        /// Human-readable error summary.
        message: String,
        /// Optional inner exception or diagnostic detail.
        detail: Option<String>,
    },

    /// Backend reported an incompatible stdio protocol version.
    #[error("Backend protocol version mismatch: expected {expected}, got {actual}")]
    ProtocolMismatch {
        /// Protocol version required by this TUI build.
        expected: u32,
        /// Protocol version reported by `mkv-rename-cli`.
        actual: u32,
    },

    /// Terminal renderer or event loop error from `OpenTUI`.
    #[error("OpenTUI error: {0}")]
    OpenTui(#[from] opentui::Error),
}
