import torch
import torch.nn as nn

from typing import Dict, Any, List

from Settings import SEQUENCE_SETTINGS, GRAPH_SETTINGS, IMAGE_SETTINGS, IEZOX_SETTINGS
from Graph.IEZOX_Graph import IEZOX_GRAPH_MULTIMODAL, IEZOX_GRAPH_CONFLUENCE
from Image.IEZOX_Image import IEZOX_IMAGE_MULTIMODAL, IEZOX_IMAGE_CONFLUENCE
from Sequence.IEZOX_Sequence import IEZOX_SEQUENCE_MULTIMODAL, IEZOX_SEQUENCE_CONFLUENCE


class IEZOX_CLASSIFIER(nn.Module):
    def __init__(self: IEZOX_CLASSIFIER, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.in_dim: int = settings["IN_DIM"]
        self.hidden_dim: int = settings["HIDDEN_DIM"]
        self.num_layers: int = settings["C_NUM_LAYERS"]
        self.dropout: float = settings["C_DROPOUT"]
        self.num_classes: int = settings["NUM_CLASSES"]

        self.in_proj = nn.Linear(self.in_dim, self.hidden_dim)
        self.hidden_layers = nn.ModuleList(
            self._build_layer(self.hidden_dim, self.hidden_dim, self.dropout)
            for _ in range(self.num_layers)
        )
        self.fc = nn.Linear(self.hidden_dim, self.num_classes)

    def _build_layer(self: IEZOX_CLASSIFIER, in_dim: int, out_dim: int, dropout: float) -> nn.Module:
        return nn.Sequential(
            nn.Linear(in_dim, out_dim),
            nn.LayerNorm(out_dim),
            nn.GELU(),
            nn.Dropout(dropout),
        )

    def forward(self: IEZOX_CLASSIFIER, x: torch.Tensor) -> torch.Tensor:
        x = self.in_proj(x)
        for layer in self.hidden_layers:
            x = layer(x)
        return self.fc(x)


class IEZOX(nn.Module):
    def __init__(self: IEZOX, settings: List[Dict[str, Any]]) -> None:
        super().__init__()
        self.SS: Dict[str, Any] = settings[0]
        self.GS: Dict[str, Any] = settings[1]
        self.IS: Dict[str, Any] = settings[2]
        self.IEZOXS: Dict[str, Any] = settings[3]

        self.graph_m = IEZOX_GRAPH_MULTIMODAL(self.GS)
        self.graph_c = IEZOX_GRAPH_CONFLUENCE(self.GS["CONFLUENCE"])

        self.image_m = IEZOX_IMAGE_MULTIMODAL(self.IS)
        self.image_c = IEZOX_IMAGE_CONFLUENCE(self.IS["CONFLUENCE"])

        self.sequence_m = IEZOX_SEQUENCE_MULTIMODAL(self.SS)
        self.sequence_c = IEZOX_SEQUENCE_CONFLUENCE(self.SS["CONFLUENCE"])

        self.d_model: int = self.IEZOXS["D_MODEL"]
        self.modal_embed = nn.Parameter(torch.randn(1, 3, self.d_model) * 0.02)

        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model,
            nhead=self.IEZOXS["NUM_HEADS"],
            dim_feedforward=self.IEZOXS["DIM_FEEDFORWARD"],
            dropout=self.IEZOXS["DROPOUT"],
            activation="gelu",
            batch_first=True,
            norm_first=True,
        )
        self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=self.IEZOXS["NUM_LAYERS"])

        self.classifier = IEZOX_CLASSIFIER(self.IEZOXS)

    def forward(self: IEZOX,
                EFG_x: torch.Tensor, EFG_edge_index: torch.Tensor, EFG_edge_attr: torch.Tensor, EFG_batch: torch.Tensor,
                IDG_x: torch.Tensor, IDG_edge_index: torch.Tensor, IDG_edge_attr: torch.Tensor, IDG_batch: torch.Tensor,
                FCG_x: torch.Tensor, FCG_edge_index: torch.Tensor, FCG_edge_attr: torch.Tensor, FCG_batch: torch.Tensor,
                BEVI_x: torch.Tensor, PESSI_x: torch.Tensor, RBGI_x: torch.Tensor,
                CMS_x: torch.Tensor, DIS_x: torch.Tensor) -> torch.Tensor:

        G1, G2, G3 = self.graph_m(EFG_x, EFG_edge_index, EFG_edge_attr, EFG_batch,
                                  IDG_x, IDG_edge_index, IDG_edge_attr, IDG_batch,
                                  FCG_x, FCG_edge_index, FCG_edge_attr, FCG_batch)
        G = self.graph_c(G1, G2, G3)

        M1, M2, M3 = self.image_m(BEVI_x, PESSI_x, RBGI_x)
        M = self.image_c(M1, M2, M3)

        S1, S2 = self.sequence_m(CMS_x, DIS_x)
        S = self.sequence_c(S1, S2)

        x = torch.stack([G, M, S], dim=1)
        x = x + self.modal_embed
        x = self.transformer(x)
        x = x.mean(dim=1)
        x = self.classifier(x)
        return x
