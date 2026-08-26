import torch
import torch.nn as nn


class ICeZeRoX_GINEConv(nn.Module):
    def __init__(self: ICeZeRoX_GINEConv, in_channels: int, out_channels: int, edge_dim: int = 1) -> None:
        super().__init__()

        self.in_channels: int = in_channels
        self.out_channels: int = out_channels
        self.edge_dim: int = edge_dim

        self.eps = nn.Parameter(torch.zeros(1))

        self.msg_mlp = nn.Sequential(
            nn.Linear(out_channels, out_channels),
            nn.GELU(),
            nn.Linear(out_channels, out_channels),
        )

        self.upd_mlp = nn.Sequential(
            nn.Linear(out_channels, out_channels),
            nn.GELU(),
            nn.Linear(out_channels, out_channels),
        )

        self.gate_proj = nn.Linear(out_channels * 2 + edge_dim, out_channels)
        self.ln = nn.LayerNorm(in_channels)
        self.res_proj = nn.Linear(in_channels, out_channels) if in_channels != out_channels else nn.Identity()

        self.init_parameters()

    def init_parameters(self: ICeZeRoX_GINEConv) -> None:
        for m in self.modules():
            if isinstance(m, nn.Linear):
                nn.init.xavier_uniform_(m.weight)
                if m.bias is not None:
                    nn.init.zeros_(m.bias)

    def forward(self: ICeZeRoX_GINEConv, x: torch.Tensor, edge_index: torch.Tensor, edge_attr: torch.Tensor) -> torch.Tensor:
        x_in = x
        x = self.ln(x)
        x = self.res_proj(x)

        row, col = edge_index
        msg_input = x[row] + edge_attr
        edge_messages = self.msg_mlp(msg_input)
        gate = self.gate_proj(torch.cat([x[row], x[col], edge_attr], dim=-1))
        edge_messages = edge_messages * gate
        agg = torch.zeros(x.size(0), self.out_channels, device=x.device, dtype=x.dtype)
        agg = torch.index_add(agg, 0, col, edge_messages)
        out = self.upd_mlp((1 + self.eps) * x + agg)

        return self.res_proj(x_in) + out
