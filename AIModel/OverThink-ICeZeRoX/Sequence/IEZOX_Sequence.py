import torch
import torch.nn as nn

from typing import Dict, Any, Tuple

from .TRANSFORMEREncoder import ICeZeRoX_TRANSFORMEREncoder


class IEZOX_SEQUENCE_MULTIMODAL(nn.Module):
    def __init__(self: IEZOX_SEQUENCE_MULTIMODAL, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.CMS: Dict[str, Any] = settings["CMS"]
        self.DIS: Dict[str, Any] = settings["DIS"]

        self.CMS_Encoder = ICeZeRoX_TRANSFORMEREncoder(
            self.CMS["VOCAB"], self.CMS["D_MODEL"], self.CMS["OUT_DIM"],
            self.CMS["NUM_LAYERS"], self.CMS["NUM_HEADS"],
            self.CMS["DIM_FEEDFORWARD"], self.CMS["DROPOUT"])
        self.DIS_Encoder = ICeZeRoX_TRANSFORMEREncoder(
            self.DIS["VOCAB"], self.DIS["D_MODEL"], self.DIS["OUT_DIM"],
            self.DIS["NUM_LAYERS"], self.DIS["NUM_HEADS"],
            self.DIS["DIM_FEEDFORWARD"], self.DIS["DROPOUT"])

    def forward(self: IEZOX_SEQUENCE_MULTIMODAL, CMS_x: torch.Tensor, DIS_x: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        CMS_x = self.CMS_Encoder(CMS_x)
        DIS_x = self.DIS_Encoder(DIS_x)
        return CMS_x, DIS_x


class IEZOX_SEQUENCE_CONFLUENCE(nn.Module):
    def __init__(self: IEZOX_SEQUENCE_CONFLUENCE, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.d_model: int = settings["D_MODEL"]
        self.in_dim: int = settings["IN_DIM"]
        self.out_dim: int = settings["OUT_DIM"]
        self.num_layers: int = settings["NUM_LAYERS"]
        self.num_heads: int = settings["NUM_HEADS"]
        self.dim_feedforward: int = settings["DIM_FEEDFORWARD"]
        self.dropout: float = settings["DROPOUT"]
        self.modality_dropout: float = settings["MODALITY_DROPOUT"]

        self.modal_embed = nn.Parameter(torch.randn(1, 2, self.d_model) * 0.02)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model, nhead=self.num_heads, dim_feedforward=self.dim_feedforward,
            dropout=self.dropout, activation="gelu", batch_first=True, norm_first=True)
        self.transformer_encoder = nn.TransformerEncoder(encoder_layer, num_layers=self.num_layers)

        self.in_proj = nn.Linear(self.in_dim, self.d_model)
        self.out_proj = nn.Linear(self.d_model, self.out_dim)

    def forward(self: IEZOX_SEQUENCE_CONFLUENCE, CMS_out: torch.Tensor, DIS_out: torch.Tensor) -> torch.Tensor:
        x = torch.stack([CMS_out, DIS_out], dim=1)
        x = self.in_proj(x)
        x = x + self.modal_embed

        if self.training and self.modality_dropout > 0:
            mask = torch.bernoulli(torch.full((x.size(0), 2), 1 - self.modality_dropout)).to(x.device)
            fill = self.modal_embed.expand(x.size(0), -1, -1)
            x = torch.where(mask.unsqueeze(-1).bool(), x, fill * 0.1)

        x = self.transformer_encoder(x)
        x = x.mean(dim=1)
        return self.out_proj(x)
