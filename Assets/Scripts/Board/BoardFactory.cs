using System.Collections.Generic;

namespace MonopolyPlanA
{
    /// <summary>
    /// Tablero clásico de Monopoly (40 casillas) con las rentas de las cartas de arte.
    /// Grupos: 0 marrón, 1 celeste, 2 rosa, 3 naranja, 4 rojo, 5 amarillo, 6 verde, 7 azul oscuro.
    /// Cada casilla comprable referencia su carta en Resources/Cards.
    /// </summary>
    public static class BoardFactory
    {
        public static List<TileData> CreateBoard()
        {
            var t = new List<TileData>(40)
            {
                new TileData("GO", TileType.Start),                                                                    // 0
                new TileData("Mediterranean Avenue", TileType.Property, 60, 2, 0, "card_brown_mediterranean_avenue"),      // 1
                new TileData("Community Chest", TileType.Community),                                                     // 2
                new TileData("Baltic Avenue", TileType.Property, 60, 4, 0, "card_brown_baltic_avenue"),                    // 3
                new TileData("Income Tax", TileType.Tax, 200),                                                // 4
                new TileData("Reading Railroad", TileType.Studio, 200, 25, -1, "card_railroad_reading"),                   // 5
                new TileData("Oriental Avenue", TileType.Property, 100, 6, 1, "card_lightblue_oriental_avenue"),           // 6
                new TileData("Chance", TileType.Chance),                                                                   // 7
                new TileData("Vermont Avenue", TileType.Property, 100, 6, 1, "card_lightblue_vermont_avenue"),             // 8
                new TileData("Connecticut Avenue", TileType.Property, 120, 8, 1, "card_lightblue_connecticut_avenue"),     // 9
                new TileData("JAIL (visiting)", TileType.Jail),                                                         // 10
                new TileData("St. Charles Place", TileType.Property, 140, 10, 2, "card_pink_st_charles_place"),            // 11
                new TileData("Electric Company", TileType.Utility, 150, 20, -1, "card_utility_electric_company"),          // 12
                new TileData("States Avenue", TileType.Property, 140, 10, 2, "card_pink_states_avenue"),                   // 13
                new TileData("Virginia Avenue", TileType.Property, 160, 12, 2, "card_pink_virginia_avenue"),               // 14
                new TileData("Pennsylvania Railroad", TileType.Studio, 200, 25, -1, "card_railroad_pennsylvania"),         // 15
                new TileData("St. James Place", TileType.Property, 180, 14, 3, "card_orange_st_james_place"),              // 16
                new TileData("Community Chest", TileType.Community),                                                     // 17
                new TileData("Tennessee Avenue", TileType.Property, 180, 14, 3, "card_orange_tennessee_avenue"),           // 18
                new TileData("New York Avenue", TileType.Property, 200, 16, 3, "card_orange_new_york_avenue"),             // 19
                new TileData("FREE PARKING", TileType.FreeParking),                                                      // 20
                new TileData("Kentucky Avenue", TileType.Property, 220, 18, 4, "card_red_kentucky_avenue"),                // 21
                new TileData("Chance", TileType.Chance),                                                                   // 22
                new TileData("Indiana Avenue", TileType.Property, 220, 18, 4, "card_red_indiana_avenue"),                  // 23
                new TileData("Illinois Avenue", TileType.Property, 240, 20, 4, "card_red_illinois_avenue"),                // 24
                new TileData("B&O Railroad", TileType.Studio, 200, 25, -1, "card_railroad_bo"),                            // 25
                new TileData("Atlantic Avenue", TileType.Property, 260, 22, 5, "card_yellow_atlantic_avenue"),             // 26
                new TileData("Ventnor Avenue", TileType.Property, 260, 22, 5, "card_yellow_ventnor_avenue"),               // 27
                new TileData("Water Works", TileType.Utility, 150, 20, -1, "card_utility_water_works"),                    // 28
                new TileData("Marvin Gardens", TileType.Property, 280, 24, 5, "card_yellow_marvin_gardens"),               // 29
                new TileData("GO TO JAIL!", TileType.GoToJail),                                                       // 30
                new TileData("Pacific Avenue", TileType.Property, 300, 26, 6, "card_green_pacific_avenue"),                // 31
                new TileData("North Carolina Avenue", TileType.Property, 300, 26, 6, "card_green_north_carolina_avenue"),  // 32
                new TileData("Community Chest", TileType.Community),                                                     // 33
                new TileData("Pennsylvania Avenue", TileType.Property, 320, 28, 6, "card_green_pennsylvania_avenue"),      // 34
                new TileData("Short Line", TileType.Studio, 200, 25, -1, "card_railroad_short_line"),                      // 35
                new TileData("Chance", TileType.Chance),                                                                   // 36
                new TileData("Park Place", TileType.Property, 350, 35, 7, "card_darkblue_park_place"),                     // 37
                new TileData("Luxury Tax", TileType.Tax, 100),                                                       // 38
                new TileData("Boardwalk", TileType.Property, 400, 50, 7, "card_darkblue_boardwalk")                        // 39
            };
            return t;
        }
    }
}
