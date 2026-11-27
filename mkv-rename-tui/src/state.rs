//! Pure wizard state and reordering logic independent of terminal widgets.

use std::collections::BTreeSet;
use std::path::Path;

use crate::protocol::{
    CaseStyle, Episode, FileImportIssue, MkvFile, RenameCandidate, RenameFileOperation,
    RenameOperationResult, RenamePlan, RenamePlanRequest, Season, Show,
};

/// Default filename template pattern prefilled in Step 3.
///
/// Uses standard `{S##E##}` season/episode padding followed by `{Title}`,
/// matching the token syntax supported by `mkv-rename-cli`.
pub const DEFAULT_PATTERN: &str = "{S##E##} {Title}";

/// Plan item status string returned by `mkv-rename-cli` when a file is ready to rename.
///
/// Must stay aligned with the `RenamePlanItem.Status` enum serialization in the C# backend.
const STATUS_READY: &str = "Ready";

/// Active step in the four-stage rename wizard.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Step {
    Search,
    Match,
    Configure,
    Results,
}

/// Active column on the Step 2 side-by-side matching screen.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MatchRailSide {
    Episodes,
    Files,
}

/// Mutable state shared across the four TUI wizard steps.
#[derive(Debug)]
pub struct WizardState {
    pub step: Step,
    pub shows: Vec<Show>,
    pub selected_show: Option<Show>,
    pub seasons: Vec<Season>,
    pub selected_season_ids: BTreeSet<i64>,
    pub episodes: Vec<Episode>,
    pub mkv_files: Vec<MkvFile>,
    pub import_issues: Vec<FileImportIssue>,
    pub active_rail: MatchRailSide,
    pub selected_episode_idx: usize,
    pub selected_file_idx: usize,
    pub pattern: String,
    pub prefix: String,
    pub case_style: CaseStyle,
    pub target_directory: Option<String>,
    pub plan: Option<RenamePlan>,
    pub results: Vec<RenameOperationResult>,
    pub can_undo: bool,
}

impl Default for WizardState {
    fn default() -> Self {
        Self {
            step: Step::Search,
            shows: Vec::new(),
            selected_show: None,
            seasons: Vec::new(),
            selected_season_ids: BTreeSet::new(),
            episodes: Vec::new(),
            mkv_files: Vec::new(),
            import_issues: Vec::new(),
            active_rail: MatchRailSide::Files,
            selected_episode_idx: 0,
            selected_file_idx: 0,
            pattern: DEFAULT_PATTERN.to_string(),
            prefix: String::new(),
            case_style: CaseStyle::Default,
            target_directory: None,
            plan: None,
            results: Vec::new(),
            can_undo: false,
        }
    }
}

impl WizardState {
    /// Creates a new `WizardState` initialized to Step 1 defaults.
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// Toggles selection of the season at `index` if present.
    pub fn toggle_season_by_index(&mut self, index: usize) {
        if let Some(season) = self.seasons.get(index)
            && !self.selected_season_ids.remove(&season.id)
        {
            self.selected_season_ids.insert(season.id);
        }
    }

    /// Moves the selected item in the active rail by `delta` (`-1` up, `1` down).
    pub fn move_active_rail_item(&mut self, delta: isize) -> bool {
        match self.active_rail {
            MatchRailSide::Episodes => {
                swap_with_delta(&mut self.episodes, &mut self.selected_episode_idx, delta)
            }
            MatchRailSide::Files => {
                swap_with_delta(&mut self.mkv_files, &mut self.selected_file_idx, delta)
            }
        }
    }

    /// Returns the number of episode/file pairs currently aligned.
    #[must_use]
    pub fn paired_count(&self) -> usize {
        self.episodes.len().min(self.mkv_files.len())
    }

    /// Builds a `RenamePlanRequest` from the current paired episodes and files.
    #[must_use]
    pub fn build_plan_request(&self) -> RenamePlanRequest {
        let count = self.paired_count();
        let mut candidates = Vec::with_capacity(count);
        for (ep, file) in self.episodes.iter().zip(self.mkv_files.iter()).take(count) {
            candidates.push(RenameCandidate {
                source_path: file.full_path.clone(),
                season: ep.season,
                episode_number: ep.episode_number,
                episode_name: ep.name.clone(),
                run_time: ep.run_time,
                air_date: ep.air_date.clone(),
            });
        }

        RenamePlanRequest {
            show_name: self
                .selected_show
                .as_ref()
                .map_or_default(|s| s.name.clone()),
            pattern: self.pattern.clone(),
            prefix: self.prefix.clone(),
            case_style: self.case_style,
            target_directory: self.target_directory.clone(),
            candidates,
        }
    }

    /// Extracts executable rename operations when the current plan is valid and conflict-free.
    #[must_use]
    pub fn build_ready_operations(&self) -> Vec<RenameFileOperation> {
        let Some(plan) = &self.plan else {
            return Vec::new();
        };

        if !plan.is_pattern_valid || plan.conflict_count > 0 {
            return Vec::new();
        }

        plan.items
            .iter()
            .filter(|item| item.status == STATUS_READY && item.target_path.is_some())
            .map(|item| RenameFileOperation {
                operation_id: item.operation_id,
                source_path: item.source_path.clone(),
                target_path: item.target_path.clone(),
            })
            .collect()
    }
}

/// Extracts the file name component of `path` for compact display.
#[must_use]
pub fn display_file_name(path: &str) -> &str {
    Path::new(path)
        .file_name()
        .and_then(|n| n.to_str())
        .unwrap_or(path)
}

fn swap_with_delta<T>(items: &mut [T], selected: &mut usize, delta: isize) -> bool {
    if items.len() < 2 || *selected >= items.len() {
        return false;
    }

    let Some(next_idx) = selected.checked_add_signed(delta) else {
        return false;
    };
    if next_idx >= items.len() {
        return false;
    }

    items.swap(*selected, next_idx);
    *selected = next_idx;
    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reorders_files_rail_within_bounds() {
        let mut state = WizardState {
            mkv_files: vec![
                MkvFile {
                    root: "s1".into(),
                    full_path: "/media/title_t00.mkv".into(),
                    size_in_bytes: Some(100),
                    is_included: true,
                },
                MkvFile {
                    root: "s1".into(),
                    full_path: "/media/title_t01.mkv".into(),
                    size_in_bytes: Some(200),
                    is_included: true,
                },
            ],
            active_rail: MatchRailSide::Files,
            selected_file_idx: 0,
            ..Default::default()
        };

        assert!(!state.move_active_rail_item(-1));
        assert!(state.move_active_rail_item(1));
        assert_eq!(state.selected_file_idx, 1);
        assert_eq!(state.mkv_files[0].full_path, "/media/title_t01.mkv");
        assert_eq!(state.mkv_files[1].full_path, "/media/title_t00.mkv");
    }

    #[test]
    fn toggles_season_selection() {
        let mut state = WizardState {
            seasons: vec![Season {
                id: 101,
                name: "Season 1".into(),
                number: 1,
                total_episode_count: 10,
            }],
            ..Default::default()
        };

        state.toggle_season_by_index(0);
        assert!(state.selected_season_ids.contains(&101));
        state.toggle_season_by_index(0);
        assert!(!state.selected_season_ids.contains(&101));
    }
}
