import torch
import torch.nn as nn
import torch.nn.functional as F
from torch_geometric.nn import global_add_pool, global_mean_pool, JumpingKnowledge

from .GINE import ICeZeRoX_GINEConv
from typing import List


class ICeZeRoX_GINEncoder(nn.Module):
    def __init__(self: ICeZeRoX_GINEncoder, in_channels: int, hidden_channels: int, edge_dim: int,
                 num_layers: int, dropout: float, jk_mode: str = "cat") -> None:
        super().__init__()

        self.in_channels: int = in_channels
        self.hidden_channels: int = hidden_channels
        self.edge_dim: int = edge_dim
        self.num_layers: int = num_layers
        self.dropout: float = dropout
        self.jk_mode: str = jk_mode

        self.node_proj = nn.Linear(in_channels, hidden_channels)

        self.convs = nn.ModuleList([
            ICeZeRoX_GINEConv(hidden_channels, hidden_channels, edge_dim=edge_dim) for _ in range(num_layers)
        ])

        self.jk = JumpingKnowledge(mode=jk_mode, channels=hidden_channels, num_layers=num_layers)

        jk_out_channels: int = hidden_channels * num_layers if jk_mode == "cat" else hidden_channels
        self.jk_proj = nn.Sequential(
            nn.Linear(jk_out_channels, hidden_channels),
            nn.GELU(),
            nn.Linear(hidden_channels, hidden_channels),
        )
        self.pool_proj = nn.Linear(hidden_channels * 2, hidden_channels)

    def forward(self: ICeZeRoX_GINEncoder, x: torch.Tensor, edge_index: torch.Tensor,
                edge_attr: torch.Tensor, batch: torch.Tensor) -> torch.Tensor:
        x = self.node_proj(x)

        xs: List[torch.Tensor] = []
        for conv in self.convs:
            x = conv(x, edge_index, edge_attr)
            x = F.dropout(x, p=self.dropout, training=self.training)
            xs.append(x)
        h = self.jk(xs)
        h = self.jk_proj(h)

        h_sum = global_add_pool(h, batch)
        h_mean = global_mean_pool(h, batch)
        h = torch.cat([h_sum, h_mean], dim=-1)
        return self.pool_proj(h)
