from typing import Optional, Tuple

import torch
from torch import nn
from torch.nn import functional as F

import numpy as np

class Bitremal(nn.Module):
    def __init__(self, num_classes: int = 2, input_dim: int = 512,
                 token_dim: int = 512, num_tokens: int = 8,
                 embed_dim: int = 512, depth: int = 4, heads: int = 8) -> None:
        super().__init__()
        self.input_dim: int = input_dim
        self.num_tokens: int = num_tokens
        self.tokenizer: nn.Conv1d = nn.Conv1d(1, token_dim, kernel_size=input_dim // num_tokens, stride=input_dim // num_tokens)
        self.feature_proj: nn.Linear = nn.Linear(token_dim, embed_dim)
        self.cls_token: nn.Parameter = nn.Parameter(torch.zeros(1, 1, embed_dim))
        self.pos_embed: nn.Parameter = nn.Parameter(torch.zeros(1, num_tokens + 1, embed_dim))
        nn.init.trunc_normal_(self.cls_token, std=0.02)
        nn.init.trunc_normal_(self.pos_embed, std=0.02)
        encoder_layer: nn.TransformerEncoderLayer = nn.TransformerEncoderLayer(
            d_model=embed_dim,
            nhead=heads,
            dim_feedforward=embed_dim * 4,
            dropout=0.0,
            activation="gelu",
            batch_first=True,
            norm_first=True,
        )
        self.transformer: nn.TransformerEncoder = nn.TransformerEncoder(encoder_layer, num_layers=depth, enable_nested_tensor=False)
        self.norm: nn.LayerNorm = nn.LayerNorm(embed_dim)
        self.head: nn.Linear = nn.Linear(embed_dim, num_classes)

    def forward(self, features: torch.Tensor) -> torch.Tensor:
        x: torch.Tensor = features.unsqueeze(1)
        x = self.tokenizer(x)
        x = x.permute(0, 2, 1)
        x = self.feature_proj(x)
        B: int = x.shape[0]
        cls_tokens: torch.Tensor = self.cls_token.expand(B, -1, -1)
        x = torch.cat([cls_tokens, x], dim=1)
        x = x + self.pos_embed
        x = self.transformer(x)
        x = self.norm(x)
        x = x[:, 0]
        x = self.head(x)
        return x