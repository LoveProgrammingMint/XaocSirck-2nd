from typing import Optional, Tuple

import torch
from torch import nn
from torch.nn import functional as F

import numpy as np

class EntropyMapEncoder(nn.Module):
    def __init__(self, output_dim: int = 128) -> None:
        super().__init__()
        self.output_dim: int = output_dim
        self.proj: nn.Conv1d = nn.Conv1d(1, 64, kernel_size=1)
        self.conv: nn.Sequential = nn.Sequential(
            nn.Conv1d(64, 64, kernel_size=3, padding=1, groups=64),
            nn.BatchNorm1d(64),
            nn.GELU(),
            nn.Conv1d(64, 128, kernel_size=1),
            nn.BatchNorm1d(128),
            nn.GELU(),
        )
        self.pool: nn.AdaptiveAvgPool1d = nn.AdaptiveAvgPool1d(1)
        self.head: nn.Linear = nn.Linear(128, output_dim)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = x.permute(0, 2, 1)
        x = self.proj(x)
        x = self.conv(x)
        x = self.pool(x).squeeze(-1)
        x = self.head(x)
        return x