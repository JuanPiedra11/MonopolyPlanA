using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    public enum CardEffectType
    {
        Money,               // cobra (+) o paga (-) Amount
        MoneyFromPlayers,    // cada jugador le paga Amount
        MoveTo,              // avanza hasta la casilla Amount (cobra al pasar SALIDA)
        MoveBack,            // retrocede Amount casillas (sin cobrar SALIDA)
        GoToJail,            // directo a la cárcel
        PayPerHouse,         // paga Amount por cada casa construida
        MoveToNearestStudio  // avanza al ferrocarril más cercano
    }

    public class EventCard
    {
        public readonly string Id;   // para el arte: Resources/Cards/event_<Id>.png
        public readonly string Text;
        public readonly CardEffectType Effect;
        public readonly int Amount;

        public EventCard(string id, string text, CardEffectType effect, int amount = 0)
        {
            Id = id;
            Text = text;
            Effect = effect;
            Amount = amount;
        }
    }

    /// <summary>Mazos de Suerte y Caja de Comunidad (se barajan al iniciar).</summary>
    public static class EventDecks
    {
        public static Queue<EventCard> CreateChance() => Shuffle(new List<EventCard>
        {
            new EventCard("chance_advance_go", "Advance to GO and collect $200.", CardEffectType.MoveTo, 0),
            new EventCard("chance_illinois",   "Advance to Illinois Avenue.", CardEffectType.MoveTo, 24),
            new EventCard("chance_boardwalk",  "Advance to Boardwalk.", CardEffectType.MoveTo, 39),
            new EventCard("chance_stcharles",  "Advance to St. Charles Place.", CardEffectType.MoveTo, 11),
            new EventCard("chance_railroad",   "Advance to the nearest railroad.", CardEffectType.MoveToNearestStudio),
            new EventCard("chance_back3",      "Go back 3 spaces.", CardEffectType.MoveBack, 3),
            new EventCard("chance_jail",       "Go directly to jail. Do not pass GO.", CardEffectType.GoToJail),
            new EventCard("chance_dividend",   "Bank pays you a dividend: collect $50.", CardEffectType.Money, 50),
            new EventCard("chance_speeding",   "Speeding fine: pay $15.", CardEffectType.Money, -15),
            new EventCard("chance_repairs",    "General repairs: pay $25 for each house you own.", CardEffectType.PayPerHouse, 25),
        });

        public static Queue<EventCard> CreateCommunity() => Shuffle(new List<EventCard>
        {
            new EventCard("community_bank_error", "Bank error in your favor: collect $200.", CardEffectType.Money, 200),
            new EventCard("community_doctor",     "Doctor's fees: pay $50.", CardEffectType.Money, -50),
            new EventCard("community_stock",      "From sale of stock: collect $50.", CardEffectType.Money, 50),
            new EventCard("community_tax_refund", "Income tax refund: collect $20.", CardEffectType.Money, 20),
            new EventCard("community_inherit",    "You inherit $100.", CardEffectType.Money, 100),
            new EventCard("community_birthday",   "It's your birthday! Each player pays you $10.", CardEffectType.MoneyFromPlayers, 10),
            new EventCard("community_hospital",   "Hospital fees: pay $100.", CardEffectType.Money, -100),
            new EventCard("community_beauty",     "Second prize in a beauty contest: collect $10.", CardEffectType.Money, 10),
            new EventCard("community_jail",       "Go directly to jail. Do not pass GO.", CardEffectType.GoToJail),
            new EventCard("community_go",         "Return to GO and collect $200.", CardEffectType.MoveTo, 0),
        });

        static Queue<EventCard> Shuffle(List<EventCard> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return new Queue<EventCard>(cards);
        }
    }
}
