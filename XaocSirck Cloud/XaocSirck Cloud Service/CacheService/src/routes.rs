use crate::cold::ColdCache;
use crate::control::{Control, BUSY};
use crate::hot::{HotCache, HotKind, NOT_FOUND};
use axum::body::Bytes;
use axum::extract::{Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Extension, Json, Router};
use common::auth::Claims;
use serde::Deserialize;
use std::sync::Arc;

pub struct AppState {
    pub hot: HotCache,
    pub cold: ColdCache,
    pub control: Control,
    pub jwt_public_key: String,
}

pub fn public_router(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/query", get(query_get).post(query_post))
        .with_state(state)
}

pub fn admin_router(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/add", post(add))
        .route("/delete", post(delete))
        .route("/get", get(get_entry).post(get_entry))
        .route("/clear", post(clear))
        .route("/build", post(build))
        .route("/hot/delete", post(delete_hot))
        .route("/stop", post(stop))
        .route("/restart", post(restart))
        .with_state(state)
}

fn bad_request(message: &'static str) -> Response {
    (StatusCode::BAD_REQUEST, message).into_response()
}

fn parse_sha256(value: &str) -> Option<[u8; 32]> {
    let bytes = hex::decode(value).ok()?;
    if bytes.len() != 32 {
        return None;
    }
    let mut out = [0u8; 32];
    out.copy_from_slice(&bytes);
    Some(out)
}

#[derive(Deserialize)]
struct QueryParam {
    sha256: String,
}

async fn query_get(
    State(state): State<Arc<AppState>>,
    Query(params): Query<QueryParam>,
) -> Response {
    query_by_value(state, params.sha256).await
}

async fn query_post(
    State(state): State<Arc<AppState>>,
    Json(params): Json<QueryParam>,
) -> Response {
    query_by_value(state, params.sha256).await
}

async fn query_by_value(state: Arc<AppState>, sha256: String) -> Response {
    if state.control.wait().await.is_err() {
        return Bytes::from_static(&[BUSY]).into_response();
    }
    let bytes = match parse_sha256(&sha256) {
        Some(v) => v,
        None => return Bytes::from_static(&[NOT_FOUND]).into_response(),
    };
    let mut result = state.hot.query(&bytes).await;
    if result == NOT_FOUND {
        if let Ok(Some(entry)) = state.cold.get(&bytes).await {
            result = entry.label as u8;
        }
    }
    Bytes::from(vec![result]).into_response()
}

#[derive(Deserialize)]
struct AddReq {
    sha256: String,
    label: i16,
}

async fn add(
    State(state): State<Arc<AppState>>,
    Extension(claims): Extension<Claims>,
    Json(body): Json<AddReq>,
) -> Result<Response, Response> {
    let bytes = parse_sha256(&body.sha256).ok_or_else(|| bad_request("invalid sha256"))?;
    match state.cold.add(&bytes, body.label, &claims.sub).await {
        Ok(()) => Ok((StatusCode::OK, "added").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct DeleteReq {
    sha256: String,
}

async fn delete(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
    Json(body): Json<DeleteReq>,
) -> Result<Response, Response> {
    let bytes = parse_sha256(&body.sha256).ok_or_else(|| bad_request("invalid sha256"))?;
    match state.cold.delete(&bytes).await {
        Ok(_) => Ok((StatusCode::OK, "deleted").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct GetReq {
    sha256: Option<String>,
    limit: Option<i64>,
    offset: Option<i64>,
}

async fn get_entry(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
    Query(params): Query<GetReq>,
) -> Result<Response, Response> {
    if let Some(sha256) = params.sha256 {
        let bytes = parse_sha256(&sha256).ok_or_else(|| bad_request("invalid sha256"))?;
        match state.cold.get(&bytes).await {
            Ok(Some(entry)) => Ok(Json(entry).into_response()),
            Ok(None) => Ok((StatusCode::NOT_FOUND, "not found").into_response()),
            Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
        }
    } else {
        let limit = params.limit.unwrap_or(1000);
        let offset = params.offset.unwrap_or(0);
        match state.cold.list(limit, offset).await {
            Ok(entries) => Ok(Json(entries).into_response()),
            Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
        }
    }
}

async fn clear(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
) -> Result<Response, Response> {
    match state.cold.clear().await {
        Ok(count) => {
            let body = serde_json::json!({ "cleared": count });
            Ok(Json(body).into_response())
        }
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct HotDeleteReq {
    kind: Option<String>,
}

async fn delete_hot(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
    Json(body): Json<HotDeleteReq>,
) -> Result<Response, Response> {
    let kind = match body.kind.as_deref() {
        Some("malicious") => Some(HotKind::Malicious),
        Some("clean") => Some(HotKind::Clean),
        None => None,
        _ => return Ok(bad_request("kind must be malicious, clean or omitted")),
    };
    match state.hot.clear(kind).await {
        Ok(()) => Ok((StatusCode::OK, "hot cache cleared").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

pub async fn build_hot_cache(state: &AppState) -> Result<serde_json::Value, String> {
    let entries = state.cold.list_all().await.map_err(|e| e.to_string())?;
    let mut mal_keys = Vec::with_capacity(entries.len() / 2);
    let mut cle_keys = Vec::with_capacity(entries.len() / 2);
    for entry in entries {
        let bytes = hex::decode(&entry.sha256).map_err(|_| "invalid sha256 in cold cache".to_string())?;
        if bytes.len() != 32 {
            return Err("invalid sha256 length in cold cache".to_string());
        }
        let mut key = [0u8; 32];
        key.copy_from_slice(&bytes);
        match entry.label {
            1 => mal_keys.push(key),
            0 => cle_keys.push(key),
            _ => {}
        }
    }
    let mal_count = mal_keys.len();
    let cle_count = cle_keys.len();
    state.hot.build(HotKind::Malicious, mal_keys).await.map_err(|e| e.to_string())?;
    state.hot.build(HotKind::Clean, cle_keys).await.map_err(|e| e.to_string())?;
    Ok(serde_json::json!({
        "malicious": mal_count,
        "clean": cle_count
    }))
}

async fn build(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
) -> Result<Response, Response> {
    match build_hot_cache(&state).await {
        Ok(body) => Ok((StatusCode::OK, Json(body)).into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error).into_response()),
    }
}

async fn stop(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
) -> Result<Response, Response> {
    state.control.stop();
    Ok((StatusCode::OK, "stopped").into_response())
}

async fn restart(
    State(state): State<Arc<AppState>>,
    Extension(_claims): Extension<Claims>,
) -> Result<Response, Response> {
    state.control.restart();
    Ok((StatusCode::OK, "restarted").into_response())
}
