using System.Collections.Generic;

namespace MonopolyPlanA
{
    /// <summary>
    /// Crea el tablero clásico de 40 casillas, tematizado con el pipeline
    /// de animación de videojuegos (PlanA technology).
    /// Grupos de color: 0 Bocetos, 1 Storyboard, 2 Modelado, 3 Texturizado,
    /// 4 Rigging, 5 Animación, 6 Iluminación, 7 Render Final.
    /// </summary>
    public static class BoardFactory
    {
        public static List<TileData> CreateBoard()
        {
            var t = new List<TileData>(40)
            {
                new TileData("SALIDA", TileType.Start),                                        // 0
                new TileData("Boceto a Lápiz", TileType.Property, 60, 6, 0),                   // 1
                new TileData("Caja de Comunidad", TileType.Community),                         // 2
                new TileData("Boceto Digital", TileType.Property, 60, 8, 0),                   // 3
                new TileData("Impuesto de Software", TileType.Tax, 200),                       // 4
                new TileData("Estudio Norte", TileType.Studio, 200, 25),                       // 5
                new TileData("Storyboard Básico", TileType.Property, 100, 10, 1),              // 6
                new TileData("Suerte", TileType.Chance),                                       // 7
                new TileData("Animatic 2D", TileType.Property, 100, 10, 1),                    // 8
                new TileData("Storyboard Cinemático", TileType.Property, 120, 12, 1),          // 9
                new TileData("CÁRCEL (Crunch)", TileType.Jail),                                // 10
                new TileData("Modelado Low-Poly", TileType.Property, 140, 14, 2),              // 11
                new TileData("Granja de Render", TileType.Utility, 150, 20),                   // 12
                new TileData("Modelado High-Poly", TileType.Property, 140, 14, 2),             // 13
                new TileData("Escultura Digital", TileType.Property, 160, 16, 2),              // 14
                new TileData("Estudio Sur", TileType.Studio, 200, 25),                         // 15
                new TileData("Texturas PBR", TileType.Property, 180, 18, 3),                   // 16
                new TileData("Caja de Comunidad", TileType.Community),                         // 17
                new TileData("UVs y Bakeo", TileType.Property, 180, 18, 3),                    // 18
                new TileData("Materiales Estilizados", TileType.Property, 200, 20, 3),         // 19
                new TileData("DESCANSO LIBRE", TileType.FreeParking),                          // 20
                new TileData("Rig de Personaje", TileType.Property, 220, 22, 4),               // 21
                new TileData("Suerte", TileType.Chance),                                       // 22
                new TileData("Rig Facial", TileType.Property, 220, 22, 4),                     // 23
                new TileData("Skinning Avanzado", TileType.Property, 240, 24, 4),              // 24
                new TileData("Estudio Este", TileType.Studio, 200, 25),                        // 25
                new TileData("Ciclo de Caminata", TileType.Property, 260, 26, 5),              // 26
                new TileData("Animación de Combate", TileType.Property, 260, 26, 5),           // 27
                new TileData("Captura de Movimiento", TileType.Utility, 150, 20),              // 28
                new TileData("Cinemática In-Game", TileType.Property, 280, 28, 5),             // 29
                new TileData("¡VE AL CRUNCH!", TileType.GoToJail),                             // 30
                new TileData("Iluminación Global", TileType.Property, 300, 30, 6),             // 31
                new TileData("Efectos Visuales", TileType.Property, 300, 30, 6),               // 32
                new TileData("Caja de Comunidad", TileType.Community),                         // 33
                new TileData("Post-Procesado", TileType.Property, 320, 32, 6),                 // 34
                new TileData("Estudio Oeste", TileType.Studio, 200, 25),                       // 35
                new TileData("Suerte", TileType.Chance),                                       // 36
                new TileData("Render 4K", TileType.Property, 350, 35, 7),                      // 37
                new TileData("Impuesto de Lujo", TileType.Tax, 100),                           // 38
                new TileData("Máster Final", TileType.Property, 400, 50, 7)                    // 39
            };
            return t;
        }
    }
}
