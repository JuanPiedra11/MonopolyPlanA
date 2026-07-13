namespace MonopolyPlanA
{
    public enum TileType
    {
        Start,        // Salida: cobra salario al pasar
        Property,     // Propiedad comprable
        Studio,       // "Railroad" tematizado: estudios de animación
        Utility,      // Servicios: granja de render / captura de movimiento
        Tax,          // Impuesto
        Chance,       // Suerte
        Community,    // Caja de comunidad
        Jail,         // Cárcel (solo de visita si caes normal)
        GoToJail,     // Ve a la cárcel
        FreeParking   // Descanso libre
    }
}
