use serde::Deserialize;
use tauri::{AppHandle, Window};
use tauri_plugin_notification::NotificationExt;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct TaskbarProgressPayload {
    status: Option<TaskbarProgressStatus>,
    progress: Option<u64>,
}

#[derive(Debug, Deserialize, Clone, Copy)]
#[serde(rename_all = "camelCase")]
enum TaskbarProgressStatus {
    None,
    Normal,
    Indeterminate,
    Paused,
    Error,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NotificationPayload {
    title: Option<String>,
    body: Option<String>,
    icon: Option<String>,
    sound: Option<String>,
}

impl From<TaskbarProgressStatus> for tauri::window::ProgressBarStatus {
    fn from(value: TaskbarProgressStatus) -> Self {
        match value {
            TaskbarProgressStatus::None => Self::None,
            TaskbarProgressStatus::Normal => Self::Normal,
            TaskbarProgressStatus::Indeterminate => Self::Indeterminate,
            TaskbarProgressStatus::Paused => Self::Paused,
            TaskbarProgressStatus::Error => Self::Error,
        }
    }
}

#[tauri::command]
fn greet(name: &str) -> String {
    format!("Hello, {}! You've been greeted from Rust!", name)
}

#[tauri::command]
fn set_taskbar_progress(
    window: Window,
    payload: TaskbarProgressPayload,
) -> Result<(), String> {
    #[cfg(desktop)]
    {
        let progress_state = tauri::window::ProgressBarState {
            status: payload.status.map(Into::into),
            progress: payload.progress,
        };

        window
            .set_progress_bar(progress_state)
            .map_err(|error| error.to_string())
    }

    #[cfg(not(desktop))]
    {
        let _ = window;
        let _ = payload;
        Err("Taskbar progress is only supported on desktop platforms.".to_string())
    }
}

#[tauri::command]
fn clear_taskbar_progress(window: Window) -> Result<(), String> {
    #[cfg(desktop)]
    {
        window
            .set_progress_bar(tauri::window::ProgressBarState {
                status: Some(tauri::window::ProgressBarStatus::None),
                progress: None,
            })
            .map_err(|error| error.to_string())
    }

    #[cfg(not(desktop))]
    {
        let _ = window;
        Err("Taskbar progress is only supported on desktop platforms.".to_string())
    }
}

#[tauri::command]
fn show_notification(
    app: AppHandle,
    payload: NotificationPayload,
) -> Result<(), String> {
    let mut notification = app.notification().builder();

    if let Some(title) = payload.title {
        notification = notification.title(title);
    }
    if let Some(body) = payload.body {
        notification = notification.body(body);
    }
    if let Some(icon) = payload.icon {
        notification = notification.icon(icon);
    }
    if let Some(sound) = payload.sound {
        notification = notification.sound(sound);
    }

    notification.show().map_err(|error| error.to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_notification::init())
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            greet,
            set_taskbar_progress,
            clear_taskbar_progress,
            show_notification
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
