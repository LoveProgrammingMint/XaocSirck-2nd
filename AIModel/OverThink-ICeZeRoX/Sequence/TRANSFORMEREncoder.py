import torch
import torch.nn as nn

from typing import Tuple


class SinusoidalPosEmb(nn.Module):
    def __init__(self: SinusoidalPosEmb, d_model: int, max_len: int = 8192) -> None:
        super().__init__()
        self.d_model: int = d_model
        self.register_buffer("pe", self._build(max_len, d_model), persistent=False)

    @staticmethod
    def _build(max_len: int, d_model: int) -> torch.Tensor:
        pe = torch.zeros(max_len, d_model)
        pos = torch.arange(max_len, dtype=torch.float).unsqueeze(1)
        div = 10000.0 ** (-torch.arange(0, d_model, 2, dtype=torch.float) / d_model)
        pe[:, 0::2] = torch.sin(pos * div)
        pe[:, 1::2] = torch.cos(pos * div)
        return pe

    def forward(self: SinusoidalPosEmb, x: torch.Tensor) -> torch.Tensor:
        seq_len: int = x.size(1)
        if seq_len > self.pe.size(0):
            self.pe = self._build(seq_len, self.d_model).to(x.device)
        return x + self.pe[:seq_len]


class ICeZeRoX_TRANSFORMEREncoder(nn.Module):
    def __init__(self: ICeZeRoX_TRANSFORMEREncoder, vocab: int, d_model: int, out_dim: int,
                 num_layers: int, num_heads: int, dim_feedforward: int, dropout: float,
                 max_len: int = 8192) -> None:
        super().__init__()
        self.vocab: int = vocab
        self.d_model: int = d_model
        self.out_dim: int = out_dim
        self.num_layers: int = num_layers
        self.num_heads: int = num_heads
        self.dim_feedforward: int = dim_feedforward
        self.dropout: float = dropout

        self.embd = nn.Embedding(vocab, d_model)
        self.cls_token = nn.Parameter(torch.randn(1, 1, d_model) * 0.02)
        self.pos_emb = SinusoidalPosEmb(d_model, max_len)
        self.drop = nn.Dropout(dropout)

        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model,
            nhead=num_heads,
            dim_feedforward=dim_feedforward,
            dropout=dropout,
            activation="gelu",
            batch_first=True,
            norm_first=True,
        )
        self.transformer_encoder = nn.TransformerEncoder(encoder_layer, num_layers=num_layers)
        self.out_proj = nn.Linear(d_model, out_dim)

    def forward(self: ICeZeRoX_TRANSFORMEREncoder, x: torch.Tensor) -> torch.Tensor:
        x = self.embd(x)
        cls = self.cls_token.expand(x.size(0), -1, -1)
        x = torch.cat([cls, x], dim=1)
        x = self.pos_emb(x)
        x = self.drop(x)
        x = self.transformer_encoder(x)
        x = x[:, 0]
        return self.out_proj(x)
