use crate::cloud::SignatureCloud;
use axum::body::Bytes;
use axum::extract::{Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Extension, Json, Router};
use common::auth::Claims;
use serde::Deserialize;
use std::sync::Arc;

pub const TRUSTED: u8 = 0;
pub const NOT_FOUND: u8 = 2;

pub struct SignatureState {
    pub cloud: SignatureCloud,
    pub jwt_public_key: String,
}

pub fn public_router(state: Arc<SignatureState>) -> Router {
    Router::new()
        .route("/query", get(query_get).post(query_post))
        .with_state(state)
}

pub fn admin_router(state: Arc<SignatureState>) -> Router {
    Router::new()
        .route("/add", post(add))
        .route("/delete", post(delete))
        .route("/get", get(get_entry).post(get_entry))
        .route("/clear", post(clear))
        .with_state(state)
}

#[derive(Deserialize)]
struct QueryParam {
    signature: String,
}

async fn query_get(
    State(state): State<Arc<SignatureState>>,
    Query(params): Query<QueryParam>,
) -> Response {
    query_by_value(state, params.signature).await
}

async fn query_post(
    State(state): State<Arc<SignatureState>>,
    Json(params): Json<QueryParam>,
) -> Response {
    query_by_value(state, params.signature).await
}

async fn query_by_value(state: Arc<SignatureState>, signature: String) -> Response {
    let result = match state.cloud.get(&signature).await {
        Ok(Some(_)) => TRUSTED,
        _ => NOT_FOUND,
    };
    Bytes::from(vec![result]).into_response()
}

#[derive(Deserialize)]
struct AddReq {
    signature: String,
}

async fn add(
    State(state): State<Arc<SignatureState>>,
    Extension(claims): Extension<Claims>,
    Json(body): Json<AddReq>,
) -> Result<Response, Response> {
    match state.cloud.add(&body.signature, &claims.sub).await {
        Ok(()) => Ok((StatusCode::OK, "added").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct DeleteReq {
    signature: String,
}

async fn delete(
    State(state): State<Arc<SignatureState>>,
    Extension(_claims): Extension<Claims>,
    Json(body): Json<DeleteReq>,
) -> Result<Response, Response> {
    match state.cloud.delete(&body.signature).await {
        Ok(_) => Ok((StatusCode::OK, "deleted").into_response()),
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}

#[derive(Deserialize)]
struct GetReq {
    signature: Option<String>,
    limit: Option<i64>,
    offset: Option<i64>,
}

async fn get_entry(
    State(state): State<Arc<SignatureState>>,
    Extension(_claims): Extension<Claims>,
    Query(params): Query<GetReq>,
) -> Result<Response, Response> {
    if let Some(signature) = params.signature {
        match state.cloud.get(&signature).await {
            Ok(Some(entry)) => Ok(Json(entry).into_response()),
            Ok(None) => Ok((StatusCode::NOT_FOUND, "not found").into_response()),
            Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
        }
    } else {
        let limit = params.limit.unwrap_or(1000);
        let offset = params.offset.unwrap_or(0);
        match state.cloud.list(limit, offset).await {
            Ok(entries) => Ok(Json(entries).into_response()),
            Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
        }
    }
}

async fn clear(
    State(state): State<Arc<SignatureState>>,
    Extension(_claims): Extension<Claims>,
) -> Result<Response, Response> {
    match state.cloud.clear().await {
        Ok(count) => {
            let body = serde_json::json!({ "cleared": count });
            Ok(Json(body).into_response())
        }
        Err(error) => Ok((StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response()),
    }
}
