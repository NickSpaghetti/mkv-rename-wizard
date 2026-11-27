//! Widget tree construction, pane highlighting, and modal/focus helpers.

use std::cell::RefCell;
use std::rc::Rc;
use std::sync::mpsc;

use opentui::prelude::{
    App, BorderStyle, Color, Container, Edge, Input, ScrollAction, ScrollBox, Select, SelectAction,
    Text, View, Widget,
};

use crate::protocol::{CaseStyle, PatternToken, RenameFileOperation};
use crate::state::{DEFAULT_PATTERN, MatchRailSide, Step, WizardState};
use crate::ui::formatting::{
    format_case_label, format_header, format_help_text, format_tokens_hint,
};
use crate::ui::worker::WorkerCommand;

/// Option index for `"Yes"` in the rename confirmation modal.
///
/// Index `0` is intentionally `"No (Cancel)"` so pressing Enter immediately never renames files.
const CONFIRM_YES_INDEX: usize = 1;

/// Tracks which focusable widget was active before opening a modal overlay.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(super) enum FocusTarget {
    SearchInput,
    ShowList,
    SeasonList,
    ScanInput,
    EpisodeRail,
    FileRail,
    PatternInput,
    PreviewList,
    ResultsList,
}

/// Shared handles to all `OpenTUI` widgets and modal states.
#[derive(Clone)]
pub(super) struct UiWidgets {
    pub muted_color: Color,
    pub header: Text,
    pub status: Text,
    pub step1_view: View,
    pub step2_view: View,
    pub step3_view: View,
    pub step4_view: View,
    pub search_pane: View,
    pub show_pane: View,
    pub season_pane: View,
    pub search_input: Input,
    pub show_list: Select,
    pub season_list: Select,
    pub scan_pane: View,
    pub episode_pane: View,
    pub file_pane: View,
    pub scan_input: Input,
    pub episode_rail: Select,
    pub file_rail: Select,
    pub pattern_pane: View,
    pub preview_pane: View,
    pub pattern_input: Input,
    pub case_label: Text,
    pub preview_list: Select,
    pub undo_button: Text,
    pub results_list: Select,
    pub help_open: Rc<RefCell<bool>>,
    pub help_return_focus: Rc<RefCell<Option<FocusTarget>>>,
    pub help_body: Text,
    pub help_scroll: ScrollBox,
    pub help_modal: View,
    pub confirm_open: Rc<RefCell<bool>>,
    pub confirm_return_focus: Rc<RefCell<Option<FocusTarget>>>,
    pub pending_ops: Rc<RefCell<Vec<RenameFileOperation>>>,
    pub confirm_message: Text,
    pub confirm_select: Select,
    pub confirm_modal: View,
}

impl UiWidgets {
    /// Builds the full 4-step layout and modal overlays and attaches them to `app.root()`.
    pub fn build(
        app: &App,
        state: &WizardState,
        initial_paths: &[String],
        tokens: &[PatternToken],
    ) -> Self {
        let muted_color = app.theme().muted;
        let header = Text::new(app, format_header(state)).fg(Color::CYAN).bold();
        let status = Text::new(
            app,
            "Step 1: Type a show name and press Enter to search TVMaze. (Ctrl+C to quit)",
        )
        .fg(muted_color);

        let (step1_view, search_pane, show_pane, season_pane, search_input, show_list, season_list) =
            build_step1(app, muted_color);
        let (step2_view, scan_pane, episode_pane, file_pane, scan_input, episode_rail, file_rail) =
            build_step2(app, muted_color, initial_paths);
        let (step3_view, pattern_pane, preview_pane, pattern_input, case_label, preview_list) =
            build_step3(app, muted_color, tokens);
        let (step4_view, undo_button, results_list) = build_step4(app, muted_color);
        let (help_modal, help_scroll, help_body) = build_help_modal(app, tokens);
        let (confirm_modal, confirm_select, confirm_message) = build_confirm_modal(app);

        app.root()
            .column()
            .add(&header)
            .add(&step1_view)
            .add(&step2_view)
            .add(&step3_view)
            .add(&step4_view)
            .add(&status)
            .add(&help_modal)
            .add(&confirm_modal);

        Self {
            muted_color,
            header,
            status,
            step1_view,
            step2_view,
            step3_view,
            step4_view,
            search_pane,
            show_pane,
            season_pane,
            search_input,
            show_list,
            season_list,
            scan_pane,
            episode_pane,
            file_pane,
            scan_input,
            episode_rail,
            file_rail,
            pattern_pane,
            preview_pane,
            pattern_input,
            case_label,
            preview_list,
            undo_button,
            results_list,
            help_open: Rc::new(RefCell::new(false)),
            help_return_focus: Rc::new(RefCell::new(None)),
            help_body,
            help_scroll,
            help_modal,
            confirm_open: Rc::new(RefCell::new(false)),
            confirm_return_focus: Rc::new(RefCell::new(None)),
            pending_ops: Rc::new(RefCell::new(Vec::new())),
            confirm_message,
            confirm_select,
            confirm_modal,
        }
    }

    /// Highlights the Step 1 Shows or Seasons pane.
    pub fn set_search_pane(&self, is_seasons: bool, focus_widget: bool) {
        self.search_pane.border_color(self.muted_color);
        if is_seasons {
            self.show_pane
                .border_color(self.muted_color)
                .title(" Shows (h, Enter) ");
            self.season_pane
                .border_color(Color::CYAN)
                .title(" * Seasons (l, Space, Enter) ");
            if focus_widget {
                self.season_list.focus();
            }
        } else {
            self.show_pane
                .border_color(Color::CYAN)
                .title(" * Shows (h, Enter) ");
            self.season_pane
                .border_color(self.muted_color)
                .title(" Seasons (l, Space, Enter) ");
            if focus_widget {
                self.show_list.focus();
            }
        }
    }

    /// Highlights the Step 1 Search input pane.
    pub fn focus_search_input(&self, focus_widget: bool) {
        self.search_pane.border_color(Color::CYAN);
        self.show_pane
            .border_color(self.muted_color)
            .title(" Shows (h, Enter) ");
        self.season_pane
            .border_color(self.muted_color)
            .title(" Seasons (l, Space, Enter) ");
        if focus_widget {
            self.search_input.focus();
        }
    }

    /// Highlights the active Step 2 rail pane and updates `state.active_rail`.
    pub fn set_active_rail(&self, state: &RefCell<WizardState>, side: MatchRailSide, focus_widget: bool) {
        state.borrow_mut().active_rail = side;
        self.scan_pane.border_color(self.muted_color);
        match side {
            MatchRailSide::Episodes => {
                self.episode_pane
                    .border_color(Color::CYAN)
                    .title(" * Episodes (h, K/J reorder) ");
                self.file_pane
                    .border_color(self.muted_color)
                    .title(" MKV Files (l, K/J reorder) ");
                if focus_widget {
                    self.episode_rail.focus();
                }
            }
            MatchRailSide::Files => {
                self.episode_pane
                    .border_color(self.muted_color)
                    .title(" Episodes (h, K/J reorder) ");
                self.file_pane
                    .border_color(Color::CYAN)
                    .title(" * MKV Files (l, K/J reorder) ");
                if focus_widget {
                    self.file_rail.focus();
                }
            }
        }
    }

    /// Highlights the Step 2 Scan Path input pane.
    pub fn focus_scan_input(&self, focus_widget: bool) {
        self.scan_pane.border_color(Color::CYAN);
        self.episode_pane
            .border_color(self.muted_color)
            .title(" Episodes (h, K/J reorder) ");
        self.file_pane
            .border_color(self.muted_color)
            .title(" MKV Files (l, K/J reorder) ");
        if focus_widget {
            self.scan_input.focus();
        }
    }

    /// Highlights the Step 3 Pattern input or Preview list pane.
    pub fn set_configure_pane(&self, is_preview: bool, focus_widget: bool) {
        if is_preview {
            self.pattern_pane
                .border_color(self.muted_color)
                .title(" Filename Pattern ");
            self.preview_pane
                .border_color(Color::CYAN)
                .title(" * Rename Preview ");
            if focus_widget {
                self.preview_list.focus();
            }
        } else {
            self.pattern_pane
                .border_color(Color::CYAN)
                .title(" * Filename Pattern ");
            self.preview_pane
                .border_color(self.muted_color)
                .title(" Rename Preview ");
            if focus_widget {
                self.pattern_input.focus();
            }
        }
    }

    /// Detects the currently focused widget, falling back to the current step's default widget.
    #[must_use]
    pub fn detect_current_focus(&self, state: &RefCell<WizardState>) -> FocusTarget {
        if self.search_input.is_focused() {
            FocusTarget::SearchInput
        } else if self.show_list.is_focused() {
            FocusTarget::ShowList
        } else if self.season_list.is_focused() {
            FocusTarget::SeasonList
        } else if self.scan_input.is_focused() {
            FocusTarget::ScanInput
        } else if self.episode_rail.is_focused() {
            FocusTarget::EpisodeRail
        } else if self.file_rail.is_focused() {
            FocusTarget::FileRail
        } else if self.pattern_input.is_focused() {
            FocusTarget::PatternInput
        } else if self.preview_list.is_focused() {
            FocusTarget::PreviewList
        } else {
            let st = state.borrow();
            match st.step {
                Step::Search => FocusTarget::SearchInput,
                Step::Match => match st.active_rail {
                    MatchRailSide::Episodes => FocusTarget::EpisodeRail,
                    MatchRailSide::Files => FocusTarget::FileRail,
                },
                Step::Configure => FocusTarget::PatternInput,
                Step::Results => FocusTarget::ResultsList,
            }
        }
    }

    /// Restores focus and border highlight to `target`.
    pub fn restore_focus(&self, state: &RefCell<WizardState>, target: FocusTarget) {
        match target {
            FocusTarget::SearchInput => self.focus_search_input(true),
            FocusTarget::ShowList => self.set_search_pane(false, true),
            FocusTarget::SeasonList => self.set_search_pane(true, true),
            FocusTarget::ScanInput => self.focus_scan_input(true),
            FocusTarget::EpisodeRail => self.set_active_rail(state, MatchRailSide::Episodes, true),
            FocusTarget::FileRail => self.set_active_rail(state, MatchRailSide::Files, true),
            FocusTarget::PatternInput => self.set_configure_pane(false, true),
            FocusTarget::PreviewList => self.set_configure_pane(true, true),
            FocusTarget::ResultsList => {
                self.results_list.focus();
            }
        }
    }

    /// Switches the visible wizard step, updates the header, and sets initial step focus.
    pub fn switch_step(&self, state: &RefCell<WizardState>, next: Step) {
        state.borrow_mut().step = next;
        self.step1_view.visible(next == Step::Search);
        self.step2_view.visible(next == Step::Match);
        self.step3_view.visible(next == Step::Configure);
        self.step4_view.visible(next == Step::Results);
        self.header.set_text(format_header(&state.borrow()));

        match next {
            Step::Search => self.focus_search_input(true),
            Step::Match => {
                let active = state.borrow().active_rail;
                self.set_active_rail(state, active, true);
            }
            Step::Configure => self.set_configure_pane(false, true),
            Step::Results => {
                self.results_list.focus();
            }
        }
    }

    /// Advances to Step 3 and dispatches a `BuildPlan` worker request.
    pub fn advance_to_configure(
        &self,
        state: &RefCell<WizardState>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let req = state.borrow().build_plan_request();
        self.switch_step(state, Step::Configure);
        let _ = cmd_tx.send(WorkerCommand::BuildPlan(req));
    }

    /// Cycles the Step 3 case style and dispatches a `BuildPlan` worker request.
    pub fn cycle_case_style(
        &self,
        state: &RefCell<WizardState>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let req = {
            let mut st = state.borrow_mut();
            st.case_style = st.case_style.next();
            self.case_label.set_text(format_case_label(st.case_style));
            st.build_plan_request()
        };
        let _ = cmd_tx.send(WorkerCommand::BuildPlan(req));
    }

    /// Opens the Confirm Rename modal (defaulting to No) if ready operations exist.
    pub fn open_confirm(&self, state: &RefCell<WizardState>) {
        let ops = state.borrow().build_ready_operations();
        if ops.is_empty() {
            self.status
                .set_text("Nothing ready to rename (fix errors/conflicts first).");
            return;
        }
        let count = ops.len();
        *self.confirm_return_focus.borrow_mut() = Some(self.detect_current_focus(state));
        *self.pending_ops.borrow_mut() = ops;
        self.confirm_message.set_text(format!(
            "Rename {count} file(s)? Press Enter to confirm selection (Esc or n cancels)."
        ));
        self.confirm_select
            .options([
                "No (Cancel)".to_string(),
                format!("Yes (Rename {count} file(s))"),
            ])
            .select(0);
        *self.confirm_open.borrow_mut() = true;
        self.confirm_modal.visible(true);
        self.confirm_select.focus();
    }

    /// Closes the Confirm Rename modal and restores the previously focused widget.
    pub fn close_confirm(&self, state: &RefCell<WizardState>) {
        *self.confirm_open.borrow_mut() = false;
        self.pending_ops.borrow_mut().clear();
        self.confirm_modal.visible(false);
        let target = self
            .confirm_return_focus
            .borrow_mut()
            .take()
            .unwrap_or(FocusTarget::PatternInput);
        self.restore_focus(state, target);
    }

    /// Dispatches an undo command if the latest rename batch can be undone.
    pub fn trigger_undo(
        &self,
        state: &RefCell<WizardState>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let to_undo = {
            let st = state.borrow();
            st.can_undo.then(|| st.results.clone())
        };
        if let Some(results) = to_undo {
            self.status.set_text("Undoing rename batch...");
            let _ = cmd_tx.send(WorkerCommand::UndoRename(results));
        } else {
            self.status.set_text("Nothing to undo.");
        }
    }

    /// Resets wizard state and clears all UI widgets for a fresh batch.
    pub fn reset_for_new_batch(&self, state: &RefCell<WizardState>) {
        *state.borrow_mut() = WizardState::new();
        self.search_input.set_value("");
        self.show_list.options(Vec::<String>::new());
        self.season_list.options(Vec::<String>::new());
        self.scan_input.set_value("");
        self.episode_rail.options(Vec::<String>::new());
        self.file_rail.options(Vec::<String>::new());
        self.pattern_input.set_value(DEFAULT_PATTERN);
        self.case_label.set_text(format_case_label(CaseStyle::Default));
        self.preview_list.options(Vec::<String>::new());
        self.results_list.options(Vec::<String>::new());
        self.undo_button.set_text(" [ Undo Rename (u) ] ");
        self.set_active_rail(state, MatchRailSide::Files, false);
        self.switch_step(state, Step::Search);
        self.status
            .set_text("Started new batch. Search for a show.");
    }

    /// Connects widget-level click, submit, change, and select handlers.
    pub fn attach_widget_handlers(
        &self,
        state: &Rc<RefCell<WizardState>>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        self.attach_focus_clicks(state);
        self.attach_step1_handlers(state, cmd_tx);
        self.attach_step2_handlers(state, cmd_tx);
        self.attach_step3_and_4_handlers(state, cmd_tx);
    }

    fn attach_focus_clicks(&self, state: &Rc<RefCell<WizardState>>) {
        let ui = self.clone();
        self.search_input.on_click(move |_cx, _m| ui.focus_search_input(false));
        let ui = self.clone();
        self.show_list.on_click(move |_cx, _m| ui.set_search_pane(false, false));
        let ui = self.clone();
        self.season_list.on_click(move |_cx, _m| ui.set_search_pane(true, false));
        let ui = self.clone();
        self.scan_input.on_click(move |_cx, _m| ui.focus_scan_input(false));
        let ui = self.clone();
        let st = Rc::clone(state);
        self.episode_rail
            .on_click(move |_cx, _m| ui.set_active_rail(&st, MatchRailSide::Episodes, false));
        let ui = self.clone();
        let st = Rc::clone(state);
        self.file_rail
            .on_click(move |_cx, _m| ui.set_active_rail(&st, MatchRailSide::Files, false));
        let ui = self.clone();
        self.pattern_input.on_click(move |_cx, _m| ui.set_configure_pane(false, false));
        let ui = self.clone();
        self.preview_list.on_click(move |_cx, _m| ui.set_configure_pane(true, false));
    }

    fn attach_step1_handlers(
        &self,
        state: &Rc<RefCell<WizardState>>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let tx = cmd_tx.clone();
        let status = self.status.clone();
        self.search_input.on_submit(move |_cx, value| {
            let trimmed = value.trim();
            if !trimmed.is_empty() {
                status.set_text(format!("Searching TVMaze for '{trimmed}'..."));
                let _ = tx.send(WorkerCommand::SearchShows(trimmed.to_string()));
            }
        });

        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        let ui = self.clone();
        self.show_list.on_select(move |_cx, index, _opt| {
            let show_id = {
                let mut guard = st.borrow_mut();
                guard.shows.get(index).cloned().map(|show| {
                    let id = show.id;
                    guard.selected_show = Some(show);
                    guard.selected_season_ids.clear();
                    id
                })
            };
            if let Some(id) = show_id {
                ui.header.set_text(format_header(&st.borrow()));
                ui.status.set_text("Loading seasons...");
                let _ = tx.send(WorkerCommand::LoadSeasons(id));
            }
        });

        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        let status = self.status.clone();
        self.season_list.on_select(move |_cx, index, _opt| {
            let ids: Vec<i64> = {
                let mut guard = st.borrow_mut();
                if guard.selected_season_ids.is_empty() {
                    guard.toggle_season_by_index(index);
                }
                guard.selected_season_ids.iter().copied().collect()
            };
            if !ids.is_empty() {
                status.set_text(format!("Loading episodes for {} season(s)...", ids.len()));
                let _ = tx.send(WorkerCommand::LoadEpisodes(ids));
            }
        });
    }

    fn attach_step2_handlers(
        &self,
        state: &Rc<RefCell<WizardState>>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let tx = cmd_tx.clone();
        let status = self.status.clone();
        self.scan_input.on_submit(move |_cx, value| {
            let paths: Vec<String> = value.split_whitespace().map(str::to_string).collect();
            if !paths.is_empty() {
                status.set_text(format!("Scanning {} path(s)...", paths.len()));
                let _ = tx.send(WorkerCommand::ScanPaths(paths));
            }
        });

        let ui = self.clone();
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.episode_rail
            .on_select(move |_cx, _idx, _opt| ui.advance_to_configure(&st, &tx));

        let ui = self.clone();
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.file_rail
            .on_select(move |_cx, _idx, _opt| ui.advance_to_configure(&st, &tx));
    }

    fn attach_step3_and_4_handlers(
        &self,
        state: &Rc<RefCell<WizardState>>,
        cmd_tx: &mpsc::Sender<WorkerCommand>,
    ) {
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.pattern_input.on_change(move |_cx, value| {
            let req = {
                let mut guard = st.borrow_mut();
                guard.pattern = value.to_string();
                guard.build_plan_request()
            };
            let _ = tx.send(WorkerCommand::BuildPlan(req));
        });

        let ui = self.clone();
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.case_label
            .on_click(move |_cx, _m| ui.cycle_case_style(&st, &tx));

        let ui = self.clone();
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.confirm_select.on_select(move |_cx, index, _opt| {
            if index == CONFIRM_YES_INDEX {
                let ops = ui.pending_ops.borrow().clone();
                let count = ops.len();
                ui.close_confirm(&st);
                if count > 0 {
                    ui.status.set_text(format!("Renaming {count} file(s)..."));
                    let _ = tx.send(WorkerCommand::ExecuteRename(ops));
                }
            } else {
                ui.close_confirm(&st);
                ui.status.set_text("Rename cancelled.");
            }
        });

        let ui = self.clone();
        let st = Rc::clone(state);
        let tx = cmd_tx.clone();
        self.undo_button
            .on_click(move |_cx, _m| ui.trigger_undo(&st, &tx));
    }
}

fn build_step1(app: &App, muted_color: Color) -> (View, View, View, View, Input, Select, Select) {
    let search_input = Input::new(app);
    let show_list = Select::new(app)
        .wrap_selection(true)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);
    let season_list = Select::new(app)
        .wrap_selection(true)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);

    let search_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(Color::CYAN)
        .title(" Search TVMaze (Enter to search) ")
        .height(3)
        .child(&search_input);
    let show_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(muted_color)
        .title(" Shows (h, Enter) ")
        .grow(1.0)
        .child(&show_list);
    let season_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(muted_color)
        .title(" Seasons (l, Space, Enter) ")
        .grow(1.0)
        .child(&season_list);

    let step1_view = View::new(app)
        .column()
        .grow(1.0)
        .child(&search_pane)
        .child(
            &View::new(app)
                .row()
                .grow(1.0)
                .child(&show_pane)
                .child(&season_pane),
        );

    (step1_view, search_pane, show_pane, season_pane, search_input, show_list, season_list)
}

fn build_step2(
    app: &App,
    muted_color: Color,
    initial_paths: &[String],
) -> (View, View, View, View, Input, Select, Select) {
    let scan_input = Input::new(app);
    if !initial_paths.is_empty() {
        scan_input.set_value(initial_paths.join(" "));
    }
    let episode_rail = Select::new(app)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);
    let file_rail = Select::new(app)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);

    let scan_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(muted_color)
        .title(" Scan MKV Path (Enter to scan) ")
        .height(3)
        .child(&scan_input);
    let episode_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(muted_color)
        .title(" Episodes (h, K/J reorder) ")
        .grow(1.0)
        .child(&episode_rail);
    let file_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(Color::CYAN)
        .title(" * MKV Files (l, K/J reorder) ")
        .grow(1.0)
        .child(&file_rail);

    let step2_view = View::new(app)
        .column()
        .grow(1.0)
        .visible(false)
        .child(&scan_pane)
        .child(
            &View::new(app)
                .row()
                .grow(1.0)
                .child(&episode_pane)
                .child(&file_pane),
        );

    (step2_view, scan_pane, episode_pane, file_pane, scan_input, episode_rail, file_rail)
}

fn build_step3(
    app: &App,
    muted_color: Color,
    tokens: &[PatternToken],
) -> (View, View, View, Input, Text, Select) {
    let pattern_input = Input::new(app).unbind("ctrl+k").unbind("ctrl+r");
    pattern_input.set_value(DEFAULT_PATTERN);
    let case_label = Text::new(app, format_case_label(CaseStyle::Default)).fg(Color::YELLOW);
    let tokens_hint = Text::new(app, format_tokens_hint(tokens)).fg(muted_color);
    let preview_list = Select::new(app)
        .wrap_selection(true)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);

    let pattern_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(Color::CYAN)
        .title(" * Filename Pattern ")
        .height(3)
        .child(&pattern_input);
    let preview_pane = View::new(app)
        .border(BorderStyle::Single)
        .border_color(muted_color)
        .title(" Rename Preview ")
        .grow(1.0)
        .child(&preview_list);

    let step3_view = View::new(app)
        .column()
        .grow(1.0)
        .visible(false)
        .child(&pattern_pane)
        .child(&case_label)
        .child(&tokens_hint)
        .child(&preview_pane);

    (step3_view, pattern_pane, preview_pane, pattern_input, case_label, preview_list)
}

fn build_step4(app: &App, muted_color: Color) -> (View, Text, Select) {
    let undo_button = Text::new(app, " [ Undo Rename (u) ] ")
        .fg(Color::YELLOW)
        .bold();
    let results_list = Select::new(app)
        .wrap_selection(true)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .grow(1.0);
    let step4_view = View::new(app)
        .column()
        .grow(1.0)
        .visible(false)
        .child(
            &View::new(app)
                .row()
                .height(1)
                .child(&undo_button)
                .child(&Text::new(app, "  Ctrl+N: New batch  |  Ctrl+C: Quit").fg(muted_color)),
        )
        .child(
            &View::new(app)
                .border(BorderStyle::Single)
                .title(" Rename Results ")
                .grow(1.0)
                .child(&results_list),
        );

    (step4_view, undo_button, results_list)
}

fn build_help_modal(app: &App, tokens: &[PatternToken]) -> (View, ScrollBox, Text) {
    let help_body = Text::new(app, format_help_text(Step::Search, tokens));
    let help_scroll = ScrollBox::new(app)
        .bind("j", ScrollAction::Down)
        .bind("k", ScrollAction::Up)
        .grow(1.0)
        .child(&help_body);
    let help_modal = View::new(app)
        .absolute()
        .position(Edge::Top, 1)
        .position(Edge::Right, 3)
        .position(Edge::Bottom, 1)
        .position(Edge::Left, 3)
        .border(BorderStyle::Rounded)
        .border_color(Color::CYAN)
        .bg(app.theme().background)
        .padding(1)
        .title(" Help & Controls (F1, ?, q, or Esc to close) ")
        .visible(false)
        .child(&help_scroll);

    (help_modal, help_scroll, help_body)
}

fn build_confirm_modal(app: &App) -> (View, Select, Text) {
    let confirm_message = Text::new(app, "Rename files?").fg(Color::YELLOW).bold();
    let confirm_select = Select::new(app)
        .options(["No (Cancel)", "Yes (Rename files)"])
        .wrap_selection(true)
        .bind("j", SelectAction::MoveDown)
        .bind("k", SelectAction::MoveUp)
        .height(4);
    let confirm_modal = View::new(app)
        .absolute()
        .position(Edge::Top, 6)
        .position(Edge::Right, 12)
        .position(Edge::Bottom, 6)
        .position(Edge::Left, 12)
        .border(BorderStyle::Rounded)
        .border_color(Color::YELLOW)
        .bg(app.theme().background)
        .padding(1)
        .title(" Confirm Rename (defaults to No) ")
        .visible(false)
        .column()
        .child(&confirm_message)
        .child(&confirm_select);

    (confirm_modal, confirm_select, confirm_message)
}
