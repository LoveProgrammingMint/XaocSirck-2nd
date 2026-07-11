use axum::{extract::State, http::StatusCode, Json};
use serde::Serialize;
use std::sync::Arc;
use sysinfo::{Disks, System};

use crate::system::SystemState;

#[derive(Serialize)]
pub struct HealthResponse {
    pub cpu: f32,
    pub ram: f32,
    pub disk: f32,
}

pub async fn health(State(_state): State<Arc<SystemState>>) -> Result<Json<HealthResponse>, StatusCode> {
    let mut sys = System::new_all();
    sys.refresh_all();

    let cpu = sys.global_cpu_usage();

    let total_ram = sys.total_memory();
    let used_ram = sys.used_memory();
    let ram = if total_ram > 0 {
        (used_ram as f32 / total_ram as f32) * 100.0
    } else {
        0.0
    };

    let disks = Disks::new_with_refreshed_list();
    let mut total_space = 0u64;
    let mut available_space = 0u64;
    for disk in disks.list() {
        total_space += disk.total_space();
        available_space += disk.available_space();
    }
    let disk = if total_space > 0 {
        ((total_space - available_space) as f32 / total_space as f32) * 100.0
    } else {
        0.0
    };

    Ok(Json(HealthResponse { cpu, ram, disk }))
}
