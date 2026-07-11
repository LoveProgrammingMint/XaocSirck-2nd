use jsonwebtoken::{decode, Algorithm, DecodingKey, Validation};
use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    pub sub: String,
    pub exp: usize,
}

pub fn verify(token: &str, public_key_pem: &[u8]) -> Result<Claims, jsonwebtoken::errors::Error> {
    let key = DecodingKey::from_rsa_pem(public_key_pem)?;
    let mut validation = Validation::new(Algorithm::RS256);
    validation.validate_aud = false;
    decode::<Claims>(token, &key, &validation).map(|data| data.claims)
}
