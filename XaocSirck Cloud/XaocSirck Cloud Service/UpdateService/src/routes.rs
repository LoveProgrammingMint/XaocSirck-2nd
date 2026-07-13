use crate::update::UpdateManager;
use axum::extract::{Multipart, State};
use axum::http::{header, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Extension, Router};
use common::auth::Claims;
use std::sync::Arc;

pub struct UpdateState {
    pub manager: UpdateManager,
}

pub fn public_router(state: Arc<UpdateState>) -> Router {
    Router::new()
        .route("/version", get(version))
        .route("/download", get(download))
        .with_state(state)
}

pub fn admin_router(state: Arc<UpdateState>) -> Router {
    Router::new()
        .route("/upload", post(upload))
        .with_state(state)
}

async fn version(State(state): State<Arc<UpdateState>>) -> Response {
    let version = state.manager.version();
    ([(header::CONTENT_TYPE, "text/plain; charset=utf-8")], version).into_response()
}

async fn download(State(state): State<Arc<UpdateState>>) -> Response {
    let path = state.manager.package_path();
    match tokio::fs::read(&path).await {
        Ok(data) => (
            [
                (header::CONTENT_TYPE, "application/octet-stream"),
                (
                    header::CONTENT_DISPOSITION,
                    "attachment; filename=\"latest.zip\"",
                ),
            ],
            data,
        )
            .into_response(),
        Err(_) => (StatusCode::NOT_FOUND, "package not found").into_response(),
    }
}

async fn upload(
    State(state): State<Arc<UpdateState>>,
    Extension(_claims): Extension<Claims>,
    mut multipart: Multipart,
) -> Result<Response, Response> {
    let mut version = String::new();
    let mut file_data = Vec::new();

    while let Ok(Some(mut field)) = multipart.next_field().await {
        let name = field.name().unwrap_or("").to_string();
        if name == "version" {
            version = field.text().await.unwrap_or_default().trim().to_string();
        } else if name == "file" {
            while let Ok(Some(chunk)) = field.chunk().await {
                file_data.extend_from_slice(&chunk);
            }
        }
    }

    if version.is_empty() {
        return Ok((StatusCode::BAD_REQUEST, "version required").into_response());
    }
    if file_data.is_empty() {
        return Ok((StatusCode::BAD_REQUEST, "file required").into_response());
    }

    match state.manager.save(&file_data, &version) {
        Ok(()) => Ok((StatusCode::OK, "uploaded").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}
