using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace BoardGame.Services
{
    // Одна карточка каталога: всё, что нужно показать на экране для одной игры.
    // Разметка CatalogView.axaml берёт значения отсюда через {Binding ИмяСвойства}.
    // Значения заполняет метод CatalogService.LoadCardsAsync.
    public class ProductCard
    {
        public int Id { get; set; }                     // product_id — чтобы открыть карточку товара
        public string Article { get; set; } = "";       // артикул
        public string Name { get; set; } = "";          // название игры
        public string Category { get; set; } = "";      // категория
        public string Description { get; set; } = "";   // описание
        public string Details { get; set; } = "";       // «2–4 игрока · 8+ · 45 мин»

        public decimal BasePrice { get; set; }          // цена из каталога
        public decimal Price { get; set; }              // цена с учётом скидки (для сортировки)
        public string PriceText { get; set; } = "";     // «2 990 руб.»
        public string OldPriceText { get; set; } = "";  // зачёркнутая цена без скидки
        public bool HasDiscount { get; set; }           // действует ли скидка

        public int Stock { get; set; }                  // остаток на складе
        public string StockText { get; set; } = "";     // «На складе: 5 шт.»
        public bool InStock => Stock > 0;

        public int SoldCount { get; set; }              // продано за прошлый месяц
        public string BadgeText { get; set; } = "";     // «Хит продаж» / «Скидка 15%» / «Нет в наличии»

        public IBrush? BadgeBackground { get; set; }    // цвет плашки
        public IBrush? BadgeForeground { get; set; }
        public IBrush? CardBackground { get; set; }     // подсветка карточки при малом остатке

        public Bitmap? Image { get; set; }              // обложка игры или заглушка
    }
}
