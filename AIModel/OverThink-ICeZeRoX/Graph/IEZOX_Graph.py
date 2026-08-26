import torch
import torch.nn as nn

from typing import Dict, Any, Tuple

from .GINEncoder import ICeZeRoX_GINEncoder


class IEZOX_GRAPH_MULTIMODAL(nn.Module):
    def __init__(self: IEZOX_GRAPH_MULTIMODAL, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.EFG: Dict[str, Any] = settings["EFG"]
        self.IDG: Dict[str, Any] = settings["IDG"]
        self.FCG: Dict[str, Any] = settings["FCG"]

        self.EFG_Encoder = ICeZeRoX_GINEncoder(
            self.EFG["IN_CHANNELS"], self.EFG["HIDDEN_CHANNELS"], self.EFG["EDGE_DIM"],
            self.EFG["NUM_LAYERS"], self.EFG["DROPOUT"], self.EFG["JK_MODE"])
        self.IDG_Encoder = ICeZeRoX_GINEncoder(
            self.IDG["IN_CHANNELS"], self.IDG["HIDDEN_CHANNELS"], self.IDG["EDGE_DIM"],
            self.IDG["NUM_LAYERS"], self.IDG["DROPOUT"], self.IDG["JK_MODE"])
        self.FCG_Encoder = ICeZeRoX_GINEncoder(
            self.FCG["IN_CHANNELS"], self.FCG["HIDDEN_CHANNELS"], self.FCG["EDGE_DIM"],
            self.FCG["NUM_LAYERS"], self.FCG["DROPOUT"], self.FCG["JK_MODE"])

    def forward(self: IEZOX_GRAPH_MULTIMODAL,
                EFG_x: torch.Tensor, EFG_edge_index: torch.Tensor, EFG_edge_attr: torch.Tensor, EFG_batch: torch.Tensor,
                IDG_x: torch.Tensor, IDG_edge_index: torch.Tensor, IDG_edge_attr: torch.Tensor, IDG_batch: torch.Tensor,
                FCG_x: torch.Tensor, FCG_edge_index: torch.Tensor, FCG_edge_attr: torch.Tensor, FCG_batch: torch.Tensor
                ) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        EFG_out = self.EFG_Encoder(EFG_x, EFG_edge_index, EFG_edge_attr, EFG_batch)
        IDG_out = self.IDG_Encoder(IDG_x, IDG_edge_index, IDG_edge_attr, IDG_batch)
        FCG_out = self.FCG_Encoder(FCG_x, FCG_edge_index, FCG_edge_attr, FCG_batch)
        return EFG_out, IDG_out, FCG_out


class IEZOX_GRAPH_CONFLUENCE(nn.Module):
    def __init__(self: IEZOX_GRAPH_CONFLUENCE, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.d_model: int = settings["D_MODEL"]
        self.in_dim: int = settings["IN_DIM"]
        self.out_dim: int = settings["OUT_DIM"]
        self.num_layers: int = settings["NUM_LAYERS"]
        self.num_heads: int = settings["NUM_HEADS"]
        self.dim_feedforward: int = settings["DIM_FEEDFORWARD"]
        self.dropout: float = settings["DROPOUT"]
        self.modality_dropout: float = settings["MODALITY_DROPOUT"]

        self.modal_embed = nn.Parameter(torch.randn(1, 3, self.d_model) * 0.02)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model, nhead=self.num_heads, dim_feedforward=self.dim_feedforward,
            dropout=self.dropout, activation="gelu", batch_first=True, norm_first=True)
        self.transformer_encoder = nn.TransformerEncoder(encoder_layer, num_layers=self.num_layers)

        self.in_proj = nn.Linear(self.in_dim, self.d_model)
        self.out_proj = nn.Linear(self.d_model, self.out_dim)

    def forward(self: IEZOX_GRAPH_CONFLUENCE, EFG_out: torch.Tensor, IDG_out: torch.Tensor, FCG_out: torch.Tensor) -> torch.Tensor:
        x = torch.stack([EFG_out, IDG_out, FCG_out], dim=1)
        x = self.in_proj(x)
        x = x + self.modal_embed

        if self.training and self.modality_dropout > 0:
            mask = torch.bernoulli(torch.full((x.size(0), 3), 1 - self.modality_dropout)).to(x.device)
            fill = self.modal_embed.expand(x.size(0), -1, -1)
            x = torch.where(mask.unsqueeze(-1).bool(), x, fill * 0.1)

        x = self.transformer_encoder(x)
        x = x.mean(dim=1)
        return self.out_proj(x)
