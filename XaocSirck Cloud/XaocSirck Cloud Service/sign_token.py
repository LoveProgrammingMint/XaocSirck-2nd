import jwt
import datetime

with open("private_key.pem", "r") as f:
    private_key = f.read()

token = jwt.encode(
    {
        "sub": "admin",
        "exp": datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=7),
    },
    private_key,
    algorithm="RS256",
)

print(token)
