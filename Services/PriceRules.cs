namespace BoardGame.Services
{
    // Правила расчёта. Здесь нет ни базы данных, ни окон — только арифметика.
    // Поэтому эти методы легко проверить отдельно от интерфейса.
    public static class PriceRules
    {
        // Скидка на игру, которую не покупали в предыдущем месяце («залежавшийся» товар), %.
        public const decimal StaleDiscount = 15m;

        // Порог «хита продаж»: продано 5 и более штук за прошлый месяц.
        public const int PopularThreshold = 5;

        // Порог «осталось мало»: на складе 3 штуки и меньше.
        public const int LowStockThreshold = 3;

        // Срок бронирования заказа (самовывоз), дней.
        public const int ReserveDays = 3;

        // Максимальное количество одной игры в заказе.
        public const int MaxPerItem = 10;

        // Границы предыдущего календарного месяца: [1-е число прошлого месяца; 1-е число текущего).
        public static (DateOnly Start, DateOnly End) PreviousMonth(DateOnly day)
        {
            DateOnly end = new DateOnly(day.Year, day.Month, 1); // 1-е число текущего месяца
            DateOnly start = end.AddMonths(-1);                  // 1-е число прошлого месяца
            return (start, end);
        }

        // Цена со скидкой: P = P0 × (1 − d / 100).
        // Скидка действует, если в прошлом месяце эту игру не покупали.
        public static decimal Price(decimal basePrice, bool soldLastMonth)
        {
            decimal discount = soldLastMonth ? 0m : StaleDiscount;
            decimal finalPrice = basePrice * (1 - discount / 100m);

            // Округляем до копеек
            return decimal.Round(finalPrice, 2, MidpointRounding.AwayFromZero);
        }

        // Сумма позиции: цена × количество.
        public static decimal LineTotal(decimal price, int quantity)
        {
            return decimal.Round(price * quantity, 2, MidpointRounding.AwayFromZero);
        }

        // Итоговая стоимость заказа: S = Σ (Pi × Qi).
        public static decimal Total(IEnumerable<(decimal Price, int Quantity)> lines)
        {
            return lines.Sum(l => LineTotal(l.Price, l.Quantity));
        }

        // Дата окончания брони: дата оформления + 3 дня.
        public static DateOnly ReserveUntil(DateOnly day) => day.AddDays(ReserveDays);

        // Текст метки популярности по количеству проданных штук за прошлый месяц.
        public static string PopularityLabel(int soldCount)
        {
            return soldCount >= PopularThreshold ? "Хит продаж" : "В наличии";
        }

        // Мало ли товара на складе (карточка подсвечивается).
        public static bool IsLowStock(int stock)
        {
            return stock > 0 && stock <= LowStockThreshold;
        }
    }
}
