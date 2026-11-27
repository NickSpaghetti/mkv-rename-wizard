//! Interactive `OpenTUI` application wiring and headless widget tests.

mod formatting;
mod keys;
mod widgets;
mod worker;

use std::cell::RefCell;
use std::rc::Rc;
use std::sync::mpsc;

use opentui::prelude::{App, AppOptions, Widget};

use crate::backend::BackendClient;
use crate::error::TuiError;
use crate::protocol::PatternToken;
use crate::state::WizardState;
use crate::ui::keys::attach_key_handlers;
use crate::ui::widgets::UiWidgets;
use crate::ui::worker::{WorkerCommand, attach_worker_replies, spawn_worker};

/// Launches the interactive four-step `OpenTUI` wizard.
///
/// # Errors
/// Returns [`TuiError`] if spawning `mkv-rename-cli serve --stdio` or running the
/// `OpenTUI` event loop fails.
pub fn run(initial_paths: Vec<String>) -> Result<(), TuiError> {
    let (backend, tokens) = BackendClient::spawn()?;
    let app = App::new(AppOptions::default())?;
    let cmd_tx = spawn_worker(backend, app.proxy());

    build_ui(&app, &cmd_tx, initial_paths, tokens);
    app.run()?;
    drop(cmd_tx);
    Ok(())
}

fn build_ui(
    app: &App,
    cmd_tx: &mpsc::Sender<WorkerCommand>,
    initial_paths: Vec<String>,
    tokens: Vec<PatternToken>,
) {
    let state = Rc::new(RefCell::new(WizardState::new()));
    let tokens = Rc::new(tokens);
    let ui = UiWidgets::build(app, &state.borrow(), &initial_paths, &tokens);

    ui.attach_widget_handlers(&state, cmd_tx);
    attach_worker_replies(app, &state, &ui);
    attach_key_handlers(app, &state, &tokens, cmd_tx, &ui);

    ui.search_input.focus();

    if !initial_paths.is_empty() {
        let _ = cmd_tx.send(WorkerCommand::ScanPaths(initial_paths));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use opentui::testing::TestApp;

    use crate::protocol::{Episode, RenameOperationResult, RenamePlan, RenamePlanItem};
    use crate::ui::worker::WorkerReply;

    fn sample_tokens() -> Vec<PatternToken> {
        vec![
            PatternToken {
                name: "S##E##".into(),
                description: "Season & Episode Padded".into(),
                example_value: "S01E09".into(),
            },
            PatternToken {
                name: "Title".into(),
                description: "Episode Title".into(),
                example_value: "Pilot".into(),
            },
        ]
    }

    fn enter_step_2(app: &TestApp) {
        let proxy = app.proxy();
        let _ = proxy.send(WorkerReply::EpisodesLoaded(Ok(vec![Episode {
            id: 1,
            name: "Pilot".into(),
            season: 1,
            episode_number: Some(1),
            run_time: 30,
            air_date: None,
        }])));
        let _ = app.render();
    }

    fn render_screen(app: &TestApp, height: u32) -> String {
        let frame = app.render();
        (0..height).map(|r| frame.row(r)).collect::<Vec<_>>().join("\n")
    }

    #[test]
    fn renders_initial_search_screen_headlessly() {
        let app = TestApp::new(100, 24);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        let frame = app.render();
        let top_row = frame.row(0);
        assert!(top_row.contains("MkvRenameWizard TUI"));
        assert!(top_row.contains("1/4 Search"));
    }

    #[test]
    fn opens_help_with_template_syntax_only_on_step_3_and_f3_goes_back() {
        let app = TestApp::new(100, 32);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        // Step 1: F2 must NOT jump to Step 3 when on Step 1
        app.press("f2");
        assert!(app.render().row(0).contains("1/4 Search"));

        // Step 1 help should NOT include Template Syntax
        app.press("f1");
        let step1_help = render_screen(&app, 32);
        assert!(step1_help.contains("Step 1"));
        assert!(!step1_help.contains("Template Syntax"));
        app.press("escape");

        // Advance to Step 2 -> Step 3 -> Help SHOULD include Template Syntax
        enter_step_2(&app);
        app.press("enter");
        app.press("f1");
        let step3_help = render_screen(&app, 32);
        assert!(step3_help.contains("Template Syntax"));
        assert!(step3_help.contains("{S##E##}"));
        app.press("escape");

        // F3 goes back from Step 3 -> Step 2 (Match Rails)
        app.press("f3");
        let back_frame = app.render();
        assert!(back_frame.row(0).contains("2/4 Match Rails"));
    }

    #[test]
    fn ctrl_k_cycles_case_style_on_configure_screen() {
        let app = TestApp::new(100, 24);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        enter_step_2(&app);
        app.press("enter");
        app.press("ctrl+k");
        assert!(render_screen(&app, 24).contains("Case: snake_case"));
    }

    #[test]
    fn enter_on_step_2_advances_to_step_3_and_confirm_modal_defaults_to_no() {
        let app = TestApp::new(100, 24);
        let (tx, rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        enter_step_2(&app);
        assert!(app.render().row(0).contains("2/4 Match Rails"));

        // Enter on Step 2 advances to Step 3
        app.press("enter");
        assert!(app.render().row(0).contains("3/4 Pattern & Preview"));

        // Inject a ready plan via app.proxy() so Ctrl+R opens the confirmation modal
        let proxy = app.proxy();
        let _ = proxy.send(WorkerReply::PlanBuilt(Ok(RenamePlan {
            is_pattern_valid: true,
            ready_count: 1,
            conflict_count: 0,
            skipped_count: 0,
            done_count: 0,
            errors: Vec::new(),
            items: vec![RenamePlanItem {
                operation_id: 1,
                source_path: "/media/t00.mkv".into(),
                source_file_name: "t00.mkv".into(),
                target_path: Some("/media/S01E01 Pilot.mkv".into()),
                target_file_name: Some("S01E01 Pilot.mkv".into()),
                status: "Ready".into(),
            }],
        })));
        let _ = app.render();

        while rx.try_recv().is_ok() {}

        // Ctrl+R opens confirmation modal defaulting to No
        app.press("ctrl+r");
        let modal_text = render_screen(&app, 24);
        assert!(modal_text.contains("Confirm Rename"));
        assert!(modal_text.contains("No (Cancel)"));

        // Pressing Enter immediately selects default "No" -> must NOT send ExecuteRename
        app.press("enter");
        let _ = app.render();
        assert!(matches!(rx.try_recv(), Err(mpsc::TryRecvError::Empty)));

        // Open again, press Down to select "Yes", then Enter -> sends ExecuteRename
        app.press("ctrl+r");
        let _ = app.render();
        app.press("down");
        app.press("enter");
        let _ = app.render();
        assert!(matches!(rx.try_recv(), Ok(WorkerCommand::ExecuteRename(ops)) if ops.len() == 1));
    }

    #[test]
    fn step_1_supports_h_l_pane_switching_and_i_slash_input_focus() {
        let app = TestApp::new(100, 24);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        app.press("escape");
        assert!(render_screen(&app, 24).contains("* Shows"));

        app.press("l");
        assert!(render_screen(&app, 24).contains("* Seasons"));

        app.press("h");
        assert!(render_screen(&app, 24).contains("* Shows"));

        app.press("i");
        let screen = render_screen(&app, 24);
        assert!(!screen.contains("* Shows"));
        assert!(!screen.contains("* Seasons"));
    }

    #[test]
    fn help_modal_restores_previous_focus_on_close() {
        let app = TestApp::new(100, 24);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        enter_step_2(&app);
        app.press("enter");
        app.press("escape");
        assert!(render_screen(&app, 24).contains("* Rename Preview"));

        app.press("?");
        assert!(render_screen(&app, 24).contains("Help & Template Syntax"));
        app.press("q");
        assert!(render_screen(&app, 24).contains("* Rename Preview"));
    }

    #[test]
    fn ctrl_n_on_results_resets_widgets_pattern_and_case_style() {
        let app = TestApp::new(100, 24);
        let (tx, _rx) = mpsc::channel();
        build_ui(&app, &tx, Vec::new(), sample_tokens());

        enter_step_2(&app);
        app.press("enter");
        app.press("ctrl+k");
        let _ = app.render();

        let proxy = app.proxy();
        let _ = proxy.send(WorkerReply::RenameExecuted(Ok(vec![
            RenameOperationResult {
                operation_id: 1,
                source_path: "/media/t00.mkv".into(),
                target_path: Some("/media/S01E01 Pilot.mkv".into()),
                is_successful: true,
                error_message: None,
            },
        ])));
        let _ = app.render();
        assert!(app.render().row(0).contains("4/4 Results"));

        app.press("ctrl+n");
        let frame = app.render();
        assert!(frame.row(0).contains("1/4 Search"));
        assert!(frame.row(0).contains("No show selected"));
        assert!(frame.row(0).contains("Files: 0 | Episodes: 0"));

        enter_step_2(&app);
        app.press("enter");
        assert!(render_screen(&app, 24).contains("Case: Default"));
    }
}
