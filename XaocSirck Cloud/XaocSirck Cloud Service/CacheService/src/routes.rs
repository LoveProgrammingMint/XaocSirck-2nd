use crate::auth::{self, Claims};
use crate::cold::ColdCache;
use crate::control::{Control, BUSY};
use crate::hot::{HotCache, HotKind, NOT_FOUND};
use axum::body::Bytes;
use axum::extract::{Multipart, Query, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Json, Router};
use serde::Deserialize;
use std::sync::Arc;

pub struct AppState {
    pub hot: HotCache,
    pub cold: ColdCache,
    pub control: Control,
    pub jwt_public_key: String,
}

pub fn router(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/query", get(query_get).post(query_post))
        .route("/update", post(update))
        .route("/add", post(add))
        .route("/delete", post(delete))
        .route("/get", get(get_entry).post(get_entry))
        .route("/clear", post(clear))
        .route("/stop", post(stop))
        .route("/restart", post(restart))
        .with_state(state)
}

fn unauthorized() -> Response {
    (StatusCode::UNAUTHORIZED, "Unauthorized").into_response()
}

fn bad_request(message: &'static str) -> Response {
    (StatusCode::BAD_REQUEST, message).into_response()
}

fn require_auth(state: &AppState, headers: &HeaderMap) -> Result<Claims, Response> {
    let header = headers
        .get("authorization")
        .and_then(|value| value.to_str().ok())
        .ok_or_else(unauthorized)?;
    let token = header.strip_prefix("Bearer ").ok_or_else(unauthorized)?;
    auth::verify(token, state.jwt_public_key.as_bytes()).map_err(|_| unauthorized())
}

fn extract_token(headers: &HeaderMap) -> String {
    headers
        .get("authorization")
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.strip_prefix("Bearer "))
        .unwrap_or("")
        .to_string()
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

async fn update(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    mut multipart: Multipart,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
    let mut kind = None::<String>;
    let mut data = Vec::<u8>::new();
    while let Ok(Some(field)) = multipart.next_field().await {
        let name = field.name().unwrap_or("").to_string();
        match field.bytes().await {
            Ok(bytes) => match name.as_str() {
                "kind" => kind = String::from_utf8(bytes.to_vec()).ok(),
                "file" => data = bytes.to_vec(),
                _ => {}
            },
            Err(_) => return Ok((StatusCode::BAD_REQUEST, "invalid field").into_response()),
        }
    }
    let kind = match kind.as_deref() {
        Some("malicious") => HotKind::Malicious,
        Some("clean") => HotKind::Clean,
        _ => return Ok(bad_request("kind must be malicious or clean")),
    };
    match state.hot.update(kind, &data).await {
        Ok(()) => Ok((StatusCode::OK, "updated").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct AddReq {
    sha256: String,
    label: i16,
}

async fn add(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(body): Json<AddReq>,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
    let bytes = parse_sha256(&body.sha256).ok_or_else(|| bad_request("invalid sha256"))?;
    let token = extract_token(&headers);
    match state.cold.add(&bytes, body.label, &token).await {
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
    headers: HeaderMap,
    Json(body): Json<DeleteReq>,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
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
    headers: HeaderMap,
    Query(params): Query<GetReq>,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
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
    headers: HeaderMap,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
    match state.cold.clear().await {
        Ok(count) => {
            let body = serde_json::json!({ "cleared": count });
            Ok(Json(body).into_response())
        }
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

async fn stop(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
    state.control.stop();
    Ok((StatusCode::OK, "stopped").into_response())
}

async fn restart(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
) -> Result<Response, Response> {
    require_auth(&state, &headers)?;
    state.control.restart();
    Ok((StatusCode::OK, "restarted").into_response())
}
