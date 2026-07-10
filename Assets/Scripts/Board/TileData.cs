using System;

namespace MonopolyPlanA
{
    [Serializable]
    public class TileData
    {
        public string Name;
        public TileType Type;
        public int Price;      // costo de compra (o monto del impuesto si Type == Tax)
        public int BaseRent;   // renta base al caer aquí
        public int ColorGroup; // -1 = sin grupo; 0..7 grupos de color
        public int OwnerIndex = -1; // -1 = banco

        public TileData(string name, TileType type, int price = 0, int baseRent = 0, int colorGroup = -1)
        {
            Name = name;
            Type = type;
            Price = price;
            BaseRent = baseRent;
            ColorGroup = colorGroup;
        }

        public bool IsBuyable => (Type == TileType.Property || Type == TileType.Studio || Type == TileType.Utility) && OwnerIndex == -1;
    }
}
