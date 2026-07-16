use crate::community::Community;
use axum::extract::State;
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Extension, Json, Router};
use cache_service::AppState as CacheState;
use common::auth::Claims;
use serde::Deserialize;
use std::sync::Arc;

pub struct CommunityState {
    pub community: Community,
    pub cache: Arc<CacheState>,
}

pub fn public_router(state: Arc<CommunityState>) -> Router {
    Router::new()
        .route("/report", post(report))
        .with_state(state)
}

pub fn admin_router(state: Arc<CommunityState>) -> Router {
    Router::new()
        .route("/get", get(get_all))
        .route("/annotation", post(annotation))
        .with_state(state)
}

#[derive(Deserialize)]
struct ReportReq {
    sha256: String,
    path: String,
}

async fn report(
    State(state): State<Arc<CommunityState>>,
    Json(body): Json<ReportReq>,
) -> Result<Response, Response> {
    match state.community.report(&body.sha256, &body.path).await {
        Ok(()) => Ok((StatusCode::OK).into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

async fn get_all(State(state): State<Arc<CommunityState>>) -> Response {
    match state.community.list().await {
        Ok(entries) => Json(entries).into_response(),
        Err(error) => (StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response(),
    }
}

#[derive(Deserialize)]
struct AnnotationReq {
    sha256: String,
    label: i16,
}

async fn annotation(
    State(state): State<Arc<CommunityState>>,
    Extension(claims): Extension<Claims>,
    Json(body): Json<AnnotationReq>,
) -> Result<Response, Response> {
    let bytes = hex::decode(&body.sha256)
        .map_err(|_| bad_request("invalid sha256"))?;
    if bytes.len() != 32 {
        return Ok(bad_request("invalid sha256 length"));
    }
    let mut key = [0u8; 32];
    key.copy_from_slice(&bytes);

    if let Err(error) = state.community.delete(&body.sha256).await {
        return Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response());
    }

    match state.cache.cold.add(&key, body.label, &claims.sub).await {
        Ok(()) => Ok((StatusCode::OK).into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

fn bad_request(message: &'static str) -> Response {
    (StatusCode::BAD_REQUEST, message).into_response()
}
