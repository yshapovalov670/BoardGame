namespace BoardGame.Services
{
    // Корзина покупателя. Хранится в памяти до оформления заказа или выхода из системы.
    // Ключ — product_id, значение — количество.
    public static class Cart
    {
        private static readonly Dictionary<int, int> items = new();

        // Событие: состав корзины изменился (MainWindow обновляет счётчик в меню).
        public static event Action? Changed;

        public static IReadOnlyDictionary<int, int> Items => items;

        // Общее количество единиц товара.
        public static int Count => items.Values.Sum();

        public static bool IsEmpty => items.Count == 0;

        public static int QuantityOf(int productId) => items.GetValueOrDefault(productId);

        // Добавить товар. stock — остаток на складе: больше него положить нельзя.
        public static void Add(int productId, int quantity, int stock)
        {
            if (quantity <= 0)
                throw new AppException("Количество должно быть больше нуля.");

            int newQuantity = QuantityOf(productId) + quantity;

            if (newQuantity > stock)
                throw new AppException($"На складе только {stock} шт. этой игры.");

            if (newQuantity > PriceRules.MaxPerItem)
                throw new AppException($"Не больше {PriceRules.MaxPerItem} шт. одной игры в заказе.");

            items[productId] = newQuantity;
            Changed?.Invoke();
        }

        // Установить количество (0 — убрать позицию).
        public static void Set(int productId, int quantity)
        {
            if (quantity <= 0)
                items.Remove(productId);
            else
                items[productId] = Math.Min(quantity, PriceRules.MaxPerItem);
            Changed?.Invoke();
        }

        public static void Remove(int productId) => Set(productId, 0);

        public static void Clear()
        {
            items.Clear();
            Changed?.Invoke();
        }
    }
}
