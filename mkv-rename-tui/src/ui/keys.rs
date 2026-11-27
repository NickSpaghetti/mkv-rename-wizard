//! Global key capture, modal shortcuts, and per-step keyboard navigation.

use std::cell::RefCell;
use std::rc::Rc;
use std::sync::mpsc;

use opentui::input::{KeyCode, KeyEvent, Modifiers};
use opentui::prelude::{App, Cx, Widget};

use crate::protocol::PatternToken;
use crate::state::{MatchRailSide, Step, WizardState};
use crate::ui::formatting::{format_help_text, refresh_rails, refresh_seasons_list, step_label};
use crate::ui::widgets::UiWidgets;
use crate::ui::worker::WorkerCommand;

/// Registers the app-level `on_key_capture` handler for modals, navigation, and actions.
pub(super) fn attach_key_handlers(
    app: &App,
    state: &Rc<RefCell<WizardState>>,
    tokens: &Rc<Vec<PatternToken>>,
    cmd_tx: &mpsc::Sender<WorkerCommand>,
    ui: &UiWidgets,
) {
    let state = Rc::clone(state);
    let tokens = Rc::clone(tokens);
    let cmd_tx = cmd_tx.clone();
    let ui = ui.clone();

    app.on_key_capture(move |cx, key| {
        // Ctrl+C (quit) and Ctrl+Z (suspend) must always work, even when a modal is open.
        if key.matches("ctrl+c") || key.matches("ctrl+z") {
            return;
        }

        let current_step = state.borrow().step;
        let typing_in_input = ui.search_input.is_focused()
            || ui.scan_input.is_focused()
            || ui.pattern_input.is_focused();

        if *ui.confirm_open.borrow() {
            handle_confirm_modal_key(&ui, &state, cx, key);
            return;
        }

        if *ui.help_open.borrow() {
            handle_help_modal_key(&ui, &state, cx, key);
            return;
        }

        if key.matches("f1") || (!typing_in_input && key.matches("?")) {
            *ui.help_return_focus.borrow_mut() = Some(ui.detect_current_focus(&state));
            ui.help_body.set_text(format_help_text(current_step, &tokens));
            ui.help_modal.title(if current_step == Step::Configure {
                " Help & Template Syntax (F1, ?, q, or Esc to close) "
            } else {
                " Help & Controls (F1, ?, q, or Esc to close) "
            });
            *ui.help_open.borrow_mut() = true;
            ui.help_modal.visible(true);
            ui.help_scroll.focus();
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }

        // Pressing Escape inside a text input leaves the input so navigation keys work.
        if typing_in_input && key.matches("escape") {
            match current_step {
                Step::Search => ui.set_search_pane(false, true),
                Step::Match => {
                    let active = state.borrow().active_rail;
                    ui.set_active_rail(&state, active, true);
                }
                Step::Configure => ui.set_configure_pane(true, true),
                Step::Results => {}
            }
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }

        if key.matches("f3") || (!typing_in_input && (key.matches("b") || key.matches("backspace"))) {
            let prev = match current_step {
                Step::Search => None,
                Step::Match => Some(Step::Search),
                Step::Configure => Some(Step::Match),
                Step::Results => Some(Step::Configure),
            };
            if let Some(target_step) = prev {
                ui.switch_step(&state, target_step);
                if target_step == Step::Configure {
                    let req = state.borrow().build_plan_request();
                    let _ = cmd_tx.send(WorkerCommand::BuildPlan(req));
                }
                ui.status
                    .set_text(format!("Moved back to {}.", step_label(target_step)));
            }
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }

        if key.matches("f2") && current_step == Step::Match {
            ui.advance_to_configure(&state, &cmd_tx);
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }

        match current_step {
            Step::Search => handle_search_key(&ui, &state, typing_in_input, cx, key),
            Step::Match => handle_match_key(&ui, &state, &cmd_tx, cx, key),
            Step::Configure => handle_configure_key(&ui, &state, &cmd_tx, typing_in_input, cx, key),
            Step::Results => handle_results_key(&ui, &state, &cmd_tx, cx, key),
        }
    });
}

fn handle_confirm_modal_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if key.matches("escape") || key.matches("n") {
        ui.close_confirm(state);
        ui.status.set_text("Rename cancelled.");
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }

    if matches!(key.code, KeyCode::Up | KeyCode::Down | KeyCode::Enter)
        || key.matches("j")
        || key.matches("k")
    {
        return;
    }

    cx.prevent_default();
    cx.stop_propagation();
}

fn handle_help_modal_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if key.matches("escape") || key.matches("f1") || key.matches("?") || key.matches("q") {
        *ui.help_open.borrow_mut() = false;
        ui.help_modal.visible(false);
        let target = ui
            .help_return_focus
            .borrow_mut()
            .take()
            .unwrap_or_else(|| ui.detect_current_focus(state));
        ui.restore_focus(state, target);
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }

    if matches!(
        key.code,
        KeyCode::Up
            | KeyCode::Down
            | KeyCode::PageUp
            | KeyCode::PageDown
            | KeyCode::Home
            | KeyCode::End
    ) || key.matches("j")
        || key.matches("k")
    {
        return;
    }

    cx.prevent_default();
    cx.stop_propagation();
}

fn handle_search_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    typing_in_input: bool,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if !typing_in_input {
        if key.matches("i") || key.matches("/") {
            ui.focus_search_input(true);
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }
        if key.matches("h") || key.matches("left") {
            ui.set_search_pane(false, true);
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }
        if key.matches("l") || key.matches("right") {
            ui.set_search_pane(true, true);
            cx.prevent_default();
            cx.stop_propagation();
            return;
        }
    }

    if key.matches("space") && ui.season_list.is_focused() {
        if !state.borrow().seasons.is_empty() {
            let idx = ui.season_list.selected();
            state.borrow_mut().toggle_season_by_index(idx);
            refresh_seasons_list(&state.borrow(), &ui.season_list);
            ui.season_list.select(idx);
        }
        cx.prevent_default();
        cx.stop_propagation();
    }
}

fn handle_match_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    cmd_tx: &mpsc::Sender<WorkerCommand>,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if ui.scan_input.is_focused() {
        return;
    }

    if key.code == KeyCode::Enter {
        ui.advance_to_configure(state, cmd_tx);
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }
    if key.matches("i") || key.matches("/") {
        ui.focus_scan_input(true);
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }
    if key.matches("h") || key.matches("left") {
        ui.set_active_rail(state, MatchRailSide::Episodes, true);
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }
    if key.matches("l") || key.matches("right") {
        ui.set_active_rail(state, MatchRailSide::Files, true);
        cx.prevent_default();
        cx.stop_propagation();
        return;
    }

    let is_alt_or_shift = key.modifiers.contains(Modifiers::ALT)
        || key.modifiers.contains(Modifiers::META)
        || key.modifiers.contains(Modifiers::SHIFT);

    let reorder_delta = if (key.code == KeyCode::Up && is_alt_or_shift)
        || key.matches("K")
        || key.matches("shift+k")
        || key.matches("[")
    {
        Some(-1)
    } else if (key.code == KeyCode::Down && is_alt_or_shift)
        || key.matches("J")
        || key.matches("shift+j")
        || key.matches("]")
    {
        Some(1)
    } else {
        None
    };

    if let Some(delta) = reorder_delta {
        let side = if ui.episode_rail.is_focused() {
            MatchRailSide::Episodes
        } else {
            MatchRailSide::Files
        };
        let moved = {
            let mut st = state.borrow_mut();
            st.active_rail = side;
            st.selected_episode_idx = ui.episode_rail.selected();
            st.selected_file_idx = ui.file_rail.selected();
            st.move_active_rail_item(delta)
        };
        if moved {
            refresh_rails(&state.borrow(), &ui.episode_rail, &ui.file_rail);
        }
        cx.prevent_default();
        cx.stop_propagation();
    }
}

fn handle_configure_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    cmd_tx: &mpsc::Sender<WorkerCommand>,
    typing_in_input: bool,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if !typing_in_input && (key.matches("i") || key.matches("/")) {
        ui.set_configure_pane(false, true);
        cx.prevent_default();
        cx.stop_propagation();
    } else if key.matches("ctrl+k") || (!typing_in_input && key.matches("c")) {
        ui.cycle_case_style(state, cmd_tx);
        cx.prevent_default();
        cx.stop_propagation();
    } else if key.matches("ctrl+r") {
        ui.open_confirm(state);
        cx.prevent_default();
        cx.stop_propagation();
    }
}

fn handle_results_key(
    ui: &UiWidgets,
    state: &RefCell<WizardState>,
    cmd_tx: &mpsc::Sender<WorkerCommand>,
    cx: &mut Cx<'_>,
    key: &KeyEvent,
) {
    if key.matches("u") || key.matches("ctrl+u") {
        ui.trigger_undo(state, cmd_tx);
        cx.prevent_default();
        cx.stop_propagation();
    } else if key.matches("ctrl+n") {
        ui.reset_for_new_batch(state);
        cx.prevent_default();
        cx.stop_propagation();
    }
}
