//! Text formatting and list-rendering helpers for the TUI screens.

use std::fmt::Write as _;

use opentui::prelude::Select;

use crate::protocol::{CaseStyle, PatternToken};
use crate::state::{Step, WizardState, display_file_name};

/// Returns the compact step indicator shown in the top header.
#[must_use]
pub const fn step_label(step: Step) -> &'static str {
    match step {
        Step::Search => "1/4 Search",
        Step::Match => "2/4 Match Rails",
        Step::Configure => "3/4 Pattern & Preview",
        Step::Results => "4/4 Results",
    }
}

/// Formats the single-line top header bar from the current `WizardState`.
#[must_use]
pub fn format_header(state: &WizardState) -> String {
    let show = state
        .selected_show
        .as_ref()
        .map_or("No show selected", |s| s.name.as_str());
    let step_tag = step_label(state.step);
    let back_hint = if state.step == Step::Search {
        ""
    } else {
        " | F3/b: Back"
    };
    format!(
        " MkvRenameWizard TUI | {step_tag} | Show: {show} | Files: {} | Episodes: {}{back_hint} | F1/?: Help ",
        state.mkv_files.len(),
        state.episodes.len()
    )
}

/// Formats the Step 3 case-style bar.
#[must_use]
pub fn format_case_label(style: CaseStyle) -> String {
    format!(
        "Case: {style} (Ctrl+K or click to cycle) | Ctrl+R: Execute rename | F1: Template syntax"
    )
}

/// Formats the single-line available template tokens hint on Step 3.
#[must_use]
pub fn format_tokens_hint(tokens: &[PatternToken]) -> String {
    if tokens.is_empty() {
        return "Tokens: {Show} {Season} {S##} {Episode} {E##} {S##E##} {Title} {Year} {RunTime} {Ext}"
            .to_string();
    }

    let names: Vec<String> = tokens.iter().map(|t| format!("{{{}}}", t.name)).collect();
    format!("Tokens: {}", names.join(" "))
}

/// Formats the contextual Help modal body for `step`.
#[must_use]
pub fn format_help_text(step: Step, tokens: &[PatternToken]) -> String {
    let page_help = match step {
        Step::Search => "\
Step 1: Search TVMaze & Select Seasons

  i or /              Focus Search box
  Esc                 Leave Search box
  h / Left            Focus Shows list
  l / Right           Focus Seasons list
  j / k / Up / Down   Move selection
  Space               Toggle season in Seasons list
  Enter               Search, load seasons, or continue",
        Step::Match => "\
Step 2: Match Episodes & MKV Files

  i or /              Focus Scan Path box
  Esc                 Leave Scan Path box
  h / Left            Focus Episodes list
  l / Right           Focus MKV Files list
  j / k / Up / Down   Move selection
  K / J               Reorder selected item up or down
  Shift+Up / Down     Reorder selected item up or down
  Alt+Up / Down       Reorder selected item up or down
  [ / ]               Reorder selected item up or down
  Enter               Scan path or continue to Step 3",
        Step::Configure => "\
Step 3: Pattern & Live Rename Preview

  i or /              Focus Filename Pattern box
  Esc                 Leave Filename Pattern box
  j / k / Up / Down   Browse preview list
  Ctrl+K / c          Cycle case style: Default, snake_case, PascalCase, camelCase
  Ctrl+R              Open rename confirmation
  Tab / Shift+Tab     Switch between Pattern box and Preview list",
        Step::Results => "\
Step 4: Rename Results

  j / k / Up / Down   Browse results
  u / Ctrl+U          Undo rename batch
  Ctrl+N              Start a new batch",
    };

    let template_section = if step == Step::Configure {
        let mut token_lines = String::from(
            "\n\nTemplate Syntax: wrap tokens in {braces}\n\n  Token        Description                Example\n",
        );
        if tokens.is_empty() {
            token_lines.push_str(concat!(
                "  {Show}       Show name                  Initial D\n",
                "  {Season}     Season Number              1\n",
                "  {S##}        Season Padded              S01\n",
                "  {Episode}    Episode Number             9\n",
                "  {E##}        Episode Padded             E09\n",
                "  {S##E##}     Season & Episode Padded    S01E09\n",
                "  {Title}      Episode Title              Act. 9: Battle to the Limit\n",
                "  {Year}       Aired Year                 1998\n",
                "  {RunTime}    Run Time                   30m\n",
                "  {Ext}        Extension                  mkv",
            ));
        } else {
            for t in tokens {
                let _ = writeln!(
                    token_lines,
                    "  {:<12} {:<26} {}",
                    format!("{{{}}}", t.name),
                    t.description,
                    t.example_value
                );
            }
            token_lines.pop();
        }
        token_lines
    } else {
        String::new()
    };

    format!(
        "{page_help}{template_section}\n\n\
Global Shortcuts\n\n\
  F1 / ?              Toggle this help modal\n\
  F3 / b / Backspace  Go back to previous step\n\
  Ctrl+C              Quit"
    )
}

/// Refreshes the Step 1 seasons `Select` widget from `WizardState`.
pub fn refresh_seasons_list(state: &WizardState, list: &Select) {
    let opts: Vec<String> = state
        .seasons
        .iter()
        .map(|s| {
            let mark = if state.selected_season_ids.contains(&s.id) {
                "[x]"
            } else {
                "[ ]"
            };
            format!("{mark} Season {} ({} eps) {}", s.number, s.total_episode_count, s.name)
                .trim_end()
                .to_string()
        })
        .collect();
    list.options(opts);
}

/// Refreshes both Step 2 rail `Select` widgets from `WizardState`.
pub fn refresh_rails(state: &WizardState, episode_rail: &Select, file_rail: &Select) {
    let ep_opts: Vec<String> = state
        .episodes
        .iter()
        .enumerate()
        .map(|(i, ep)| {
            let code = ep.episode_number.map_or_else(
                || format!("S{:02}E??", ep.season),
                |n| format!("S{:02}E{n:02}", ep.season),
            );
            format!("{:>2}. {} {}", i + 1, code, ep.name)
        })
        .collect();
    let file_opts: Vec<String> = state
        .mkv_files
        .iter()
        .enumerate()
        .map(|(i, f)| format!("{:>2}. {}", i + 1, display_file_name(&f.full_path)))
        .collect();
    episode_rail.options(ep_opts);
    file_rail.options(file_opts);
    if !state.episodes.is_empty() {
        episode_rail.select(state.selected_episode_idx.min(state.episodes.len() - 1));
    }
    if !state.mkv_files.is_empty() {
        file_rail.select(state.selected_file_idx.min(state.mkv_files.len() - 1));
    }
}
