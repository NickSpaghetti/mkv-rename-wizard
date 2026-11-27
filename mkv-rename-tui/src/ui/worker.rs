//! Background RPC worker thread and UI-thread reply dispatch.

use std::cell::RefCell;
use std::rc::Rc;
use std::sync::mpsc;

use opentui::prelude::{App, AppProxy};

use crate::backend::BackendClient;
use crate::error::TuiError;
use crate::protocol::{
    Episode, FileImportResult, RenameFileOperation, RenameOperationResult, RenamePlan,
    RenamePlanRequest, Season, Show,
};
use crate::state::{Step, WizardState, display_file_name};
use crate::ui::formatting::{format_header, refresh_rails, refresh_seasons_list};
use crate::ui::widgets::UiWidgets;

/// Commands sent from the UI thread to the background backend worker.
#[derive(Debug)]
pub(super) enum WorkerCommand {
    SearchShows(String),
    LoadSeasons(i64),
    LoadEpisodes(Vec<i64>),
    ScanPaths(Vec<String>),
    BuildPlan(RenamePlanRequest),
    ExecuteRename(Vec<RenameFileOperation>),
    UndoRename(Vec<RenameOperationResult>),
}

/// Replies sent from the background worker back to the UI thread.
#[derive(Debug)]
pub(super) enum WorkerReply {
    ShowsLoaded(Result<Vec<Show>, TuiError>),
    SeasonsLoaded(Result<Vec<Season>, TuiError>),
    EpisodesLoaded(Result<Vec<Episode>, TuiError>),
    ScanCompleted(Result<FileImportResult, TuiError>),
    PlanBuilt(Result<RenamePlan, TuiError>),
    RenameExecuted(Result<Vec<RenameOperationResult>, TuiError>),
    UndoExecuted(Result<Vec<RenameOperationResult>, TuiError>),
}

/// Spawns the background worker thread owning `BackendClient`.
pub(super) fn spawn_worker(backend: BackendClient, proxy: AppProxy) -> mpsc::Sender<WorkerCommand> {
    let (cmd_tx, cmd_rx) = mpsc::channel::<WorkerCommand>();

    std::thread::spawn(move || {
        let mut client = backend;
        while let Ok(cmd) = cmd_rx.recv() {
            let reply = match cmd {
                WorkerCommand::SearchShows(q) => {
                    WorkerReply::ShowsLoaded(client.search_shows(&q))
                }
                WorkerCommand::LoadSeasons(show_id) => {
                    WorkerReply::SeasonsLoaded(client.get_seasons(show_id))
                }
                WorkerCommand::LoadEpisodes(season_ids) => {
                    let mut all = Vec::new();
                    let mut err = None;
                    for sid in season_ids {
                        match client.get_episodes(sid) {
                            Ok(mut eps) => all.append(&mut eps),
                            Err(e) => {
                                err = Some(e);
                                break;
                            }
                        }
                    }
                    WorkerReply::EpisodesLoaded(err.map_or(Ok(all), Err))
                }
                WorkerCommand::ScanPaths(paths) => {
                    WorkerReply::ScanCompleted(client.scan_paths(&paths))
                }
                WorkerCommand::BuildPlan(req) => {
                    WorkerReply::PlanBuilt(client.build_plan(&req))
                }
                WorkerCommand::ExecuteRename(ops) => {
                    WorkerReply::RenameExecuted(client.execute_rename(&ops))
                }
                WorkerCommand::UndoRename(results) => {
                    WorkerReply::UndoExecuted(client.undo_rename(&results))
                }
            };

            if proxy.send(reply).is_err() {
                break;
            }
        }
    });

    cmd_tx
}

/// Registers the UI-thread message handler for incoming `WorkerReply` messages.
pub(super) fn attach_worker_replies(app: &App, state: &Rc<RefCell<WizardState>>, ui: &UiWidgets) {
    let state = Rc::clone(state);
    let ui = ui.clone();

    app.on_message(move |_cx, reply: &WorkerReply| match reply {
        WorkerReply::ShowsLoaded(Ok(shows)) => handle_shows_loaded(&state, &ui, shows),
        WorkerReply::ShowsLoaded(Err(err)) => {
            ui.status.set_text(format!("Search error: {err}"));
        }
        WorkerReply::SeasonsLoaded(Ok(seasons)) => handle_seasons_loaded(&state, &ui, seasons),
        WorkerReply::SeasonsLoaded(Err(err)) => {
            ui.status.set_text(format!("Seasons error: {err}"));
        }
        WorkerReply::EpisodesLoaded(Ok(episodes)) => handle_episodes_loaded(&state, &ui, episodes),
        WorkerReply::EpisodesLoaded(Err(err)) => {
            ui.status.set_text(format!("Episodes error: {err}"));
        }
        WorkerReply::ScanCompleted(Ok(result)) => handle_scan_completed(&state, &ui, result),
        WorkerReply::ScanCompleted(Err(err)) => {
            ui.status.set_text(format!("Scan error: {err}"));
        }
        WorkerReply::PlanBuilt(Ok(plan)) => handle_plan_built(&state, &ui, plan),
        WorkerReply::PlanBuilt(Err(err)) => {
            ui.status.set_text(format!("Plan error: {err}"));
        }
        WorkerReply::RenameExecuted(Ok(results)) => handle_rename_executed(&state, &ui, results),
        WorkerReply::RenameExecuted(Err(err)) => {
            ui.status.set_text(format!("Rename error: {err}"));
        }
        WorkerReply::UndoExecuted(Ok(results)) => handle_undo_executed(&state, &ui, results),
        WorkerReply::UndoExecuted(Err(err)) => {
            ui.status.set_text(format!("Undo error: {err}"));
        }
    });
}

fn handle_shows_loaded(state: &RefCell<WizardState>, ui: &UiWidgets, shows: &[Show]) {
    let opts: Vec<String> = shows
        .iter()
        .map(|s| {
            if s.year_range.is_empty() {
                s.name.clone()
            } else {
                format!("{} ({}) [{}]", s.name, s.year_range, s.air_network_label)
            }
        })
        .collect();
    state.borrow_mut().shows = shows.to_vec();
    ui.show_list.options(opts);
    ui.set_search_pane(false, true);
    ui.status
        .set_text(format!("Found {} show(s). Press Enter to load seasons.", shows.len()));
}

fn handle_seasons_loaded(state: &RefCell<WizardState>, ui: &UiWidgets, seasons: &[Season]) {
    {
        let mut st = state.borrow_mut();
        st.seasons = seasons.to_vec();
        st.selected_season_ids.clear();
        if let Some(first) = seasons.first() {
            st.selected_season_ids.insert(first.id);
        }
    }
    refresh_seasons_list(&state.borrow(), &ui.season_list);
    ui.set_search_pane(true, true);
    ui.status.set_text(format!(
        "Loaded {} season(s). Space toggles selection, Enter loads episodes.",
        seasons.len()
    ));
}

fn handle_episodes_loaded(state: &RefCell<WizardState>, ui: &UiWidgets, episodes: &[Episode]) {
    let mut st = state.borrow_mut();
    st.episodes = episodes.to_vec();
    st.selected_episode_idx = 0;
    drop(st);
    refresh_rails(&state.borrow(), &ui.episode_rail, &ui.file_rail);
    ui.switch_step(state, Step::Match);
    let paired = state.borrow().paired_count();
    ui.status.set_text(format!(
        "Loaded {} episode(s), {paired} paired. h/l switch table, j/k move, K/J reorder, Enter continue.",
        episodes.len()
    ));
}

fn handle_scan_completed(state: &RefCell<WizardState>, ui: &UiWidgets, result: &FileImportResult) {
    let mut st = state.borrow_mut();
    st.mkv_files.clone_from(&result.imported_files);
    st.import_issues.clone_from(&result.file_import_issues);
    st.selected_file_idx = 0;
    drop(st);

    let st = state.borrow();
    refresh_rails(&st, &ui.episode_rail, &ui.file_rail);
    ui.header.set_text(format_header(&st));
    drop(st);
    ui.status.set_text(format!(
        "Scanned {} MKV file(s), {} issue(s).",
        result.imported_files.len(),
        result.file_import_issues.len()
    ));
}

fn handle_plan_built(state: &RefCell<WizardState>, ui: &UiWidgets, plan: &RenamePlan) {
    let rows: Vec<String> = plan
        .items
        .iter()
        .map(|item| {
            format!(
                "[{}] {} -> {}",
                item.status,
                item.source_file_name,
                item.target_file_name.as_deref().unwrap_or("(error)")
            )
        })
        .collect();
    ui.preview_list.options(rows);
    if plan.is_pattern_valid {
        ui.status.set_text(format!(
            "Ready: {} | Conflicts: {} | Skipped: {} | Done: {} | Ctrl+R: rename",
            plan.ready_count, plan.conflict_count, plan.skipped_count, plan.done_count
        ));
    } else {
        let msg = plan.errors.first().map_or_else(
            || "Pattern error".to_string(),
            |e| {
                e.suggestion.as_ref().map_or_else(
                    || format!("Invalid token '{{{}}}' (col {})", e.token_name, e.column),
                    |hint| {
                        format!(
                            "Invalid token '{{{}}}' (col {}): did you mean '{{{hint}}}'?",
                            e.token_name, e.column
                        )
                    },
                )
            },
        );
        ui.status.set_text(msg);
    }
    state.borrow_mut().plan = Some(plan.clone());
}

fn handle_rename_executed(
    state: &RefCell<WizardState>,
    ui: &UiWidgets,
    results: &[RenameOperationResult],
) {
    let ok = results.iter().filter(|r| r.is_successful).count();
    let fail = results.len() - ok;
    let rows: Vec<String> = results
        .iter()
        .map(|r| {
            if r.is_successful {
                format!(
                    "[OK] {} -> {}",
                    display_file_name(&r.source_path),
                    r.target_path.as_deref().map_or("", display_file_name)
                )
            } else {
                format!(
                    "[FAIL] {}: {}",
                    display_file_name(&r.source_path),
                    r.error_message.as_deref().unwrap_or("unknown error")
                )
            }
        })
        .collect();
    ui.results_list.options(rows);
    let mut st = state.borrow_mut();
    st.results = results.to_vec();
    st.can_undo = ok > 0;
    drop(st);
    ui.undo_button.set_text(if ok > 0 {
        " [ Undo Rename (u) ] "
    } else {
        " [ Nothing to Undo ] "
    });
    ui.switch_step(state, Step::Results);
    ui.status.set_text(format!(
        "Rename complete: {ok} succeeded, {fail} failed. Press 'u' to undo."
    ));
}

fn handle_undo_executed(
    state: &RefCell<WizardState>,
    ui: &UiWidgets,
    results: &[RenameOperationResult],
) {
    let ok = results.iter().filter(|r| r.is_successful).count();
    let fail = results.len() - ok;
    let rows: Vec<String> = results
        .iter()
        .map(|r| {
            if r.is_successful {
                format!(
                    "[UNDONE] {} -> {}",
                    display_file_name(&r.source_path),
                    r.target_path.as_deref().map_or("", display_file_name)
                )
            } else {
                format!(
                    "[UNDO FAIL] {}: {}",
                    display_file_name(&r.source_path),
                    r.error_message.as_deref().unwrap_or("unknown error")
                )
            }
        })
        .collect();
    ui.results_list.options(rows);
    let mut st = state.borrow_mut();
    st.results = results.to_vec();
    st.can_undo = false;
    drop(st);
    ui.undo_button.set_text(" [ Undone ] ");
    ui.status
        .set_text(format!("Undo complete: {ok} restored, {fail} failed."));
}
