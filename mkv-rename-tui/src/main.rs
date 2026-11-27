//! `OpenTUI` terminal frontend and headless CLI entrypoint for `MkvRenameWizard`.

mod backend;
mod error;
mod protocol;
mod state;
mod ui;

use std::io::IsTerminal;
use std::process::{ExitCode, Stdio};

use mimalloc::MiMalloc;

#[global_allocator]
static GLOBAL: MiMalloc = MiMalloc;

/// Exit code returned when invoked without a TTY and without `--headless`.
///
/// Follows the POSIX/sysexits convention of `2` for CLI invocation/usage errors.
const EXIT_USAGE_ERROR: u8 = 2;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();

    if let Some(first) = args.first().map(String::as_str) {
        if first == "--headless" || first == "-H" {
            return run_headless(&args[1..]);
        }

        if first == "--help" || first == "-h" {
            print_usage();
            return ExitCode::SUCCESS;
        }
    }

    if !std::io::stdin().is_terminal() || !std::io::stdout().is_terminal() {
        eprintln!(
            "mkv-rename-tui requires an interactive terminal. Use `mkv-rename-tui --headless <command>` for non-interactive mode."
        );
        return ExitCode::from(EXIT_USAGE_ERROR);
    }

    match ui::run(args) {
        Ok(()) => ExitCode::SUCCESS,
        Err(err) => {
            eprintln!("mkv-rename-tui error: {err}");
            ExitCode::FAILURE
        }
    }
}

fn run_headless(args: &[String]) -> ExitCode {
    let mut cmd = backend::resolve_backend_executable();
    if args.is_empty() {
        cmd.arg("--help");
    } else {
        cmd.args(args);
    }

    match cmd
        .stdin(Stdio::inherit())
        .stdout(Stdio::inherit())
        .stderr(Stdio::inherit())
        .status()
    {
        Ok(status) => status.code().map_or(ExitCode::FAILURE, |code| {
            ExitCode::from(u8::try_from(code.clamp(0, 255)).unwrap_or(1))
        }),
        Err(err) => {
            eprintln!("mkv-rename-tui headless error: failed to launch mkv-rename-cli: {err}");
            ExitCode::FAILURE
        }
    }
}

fn print_usage() {
    println!("mkv-rename-tui: Terminal UI and headless wrapper for MkvRenameWizard");
    println!();
    println!("Usage:");
    println!("  mkv-rename-tui [path...]                 Launch interactive TUI (optionally pre-scanning paths)");
    println!("  mkv-rename-tui --headless <command...>   Run non-interactive CLI mode (all 4 stages)");
    println!();
    println!("Headless Examples:");
    println!("  mkv-rename-tui --headless search \"Initial D\" --json");
    println!("  mkv-rename-tui --headless plan /media --show-id 10 --season 1 --order 2,1,3");
    println!("  mkv-rename-tui --headless plan /media --show-id 10 --season 1 --input order.json");
    println!("  mkv-rename-tui --headless apply <sessionId> --yes");
    println!("  mkv-rename-tui --headless undo <sessionId> --yes");
    println!("  mkv-rename-tui --headless sessions list");
}
