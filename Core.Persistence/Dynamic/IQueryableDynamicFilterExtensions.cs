using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Dynamic;

public static class IQueryableDynamicFilterExtensions
{
    // Ascending ve descending sorting türlerini içeren array
    private static readonly string[] _orders = { "asc", "desc" };

    // "And" ve "Or" logics içeren array
    private static readonly string[] _logics = { "and", "or" };

    // filter operatörlerini ve karşılık geldikleri LINQ ifadelerini içeren dictionary
    private static readonly IDictionary<string, string> _operators = new Dictionary<string, string>
    {
        { "eq", "=" },
        { "neq", "!=" },
        { "lt", "<" },
        { "lte", "<=" },
        { "gt", ">" },
        { "gte", ">=" },
        { "isnull", "== null" },
        { "isnotnull", "!= null" },
        { "startswith", "StartsWith" },
        { "endswith", "EndsWith" },
        { "contains", "Contains" },
        { "doesnotcontain", "Contains" }
    };

    // IQueryable üzerinde dynamic filter ve sorting sağlayan extension metodu
    public static IQueryable<T> ToDynamic<T>(this IQueryable<T> query, DynamicQuery dynamicQuery)
    {
        // Eğer filter varsa, filter işlemini gerçekleştir
        if (dynamicQuery.Filter is not null)
            query = Filter(query, dynamicQuery.Filter);

        // Eğer sorting varsa ve sorting listesi boş değilse, sorting işlemini gerçekleştir
        if (dynamicQuery.Sort is not null && dynamicQuery.Sort.Any())
            query = Sort(query, dynamicQuery.Sort);
        return query;
    }

    // IQueryable üzerinde filter işlemini gerçekleştiren yardımcı metot
    private static IQueryable<T> Filter<T>(IQueryable<T> queryable, Filter filter)
    {
        // Tüm filters topla
        IList<Filter> filters = GetAllFilters(filter);

        // filter değerlerini içeren array
        string?[] values = filters.Select(f => f.Value).ToArray();

        // filter ifadesini oluştur
        string where = Transform(filter, filters);

        // Eğer filter ifadesi boş değilse ve values array null değilse, filter uygula
        if (!string.IsNullOrEmpty(where) && values != null)
            queryable = queryable.Where(where, values);

        return queryable;
    }

    // IQueryable üzerinde sorting işlemini gerçekleştiren yardımcı metot
    private static IQueryable<T> Sort<T>(IQueryable<T> queryable, IEnumerable<Sort> sort)
    {
        // sorting fields ve alanları kontrol et
        foreach (Sort item in sort)
        {
            if (string.IsNullOrEmpty(item.Field))
                throw new ArgumentException("Invalid Field");
            if (string.IsNullOrEmpty(item.Dir) || !_orders.Contains(item.Dir))
                throw new ArgumentException("Invalid Order Type");
        }

        // Sorting array boş değilse, sorting işlemini gerçekleştir
        if (sort.Any())
        {
            // Sorting ifadesini oluştur ve uygula
            string ordering = string.Join(separator: ",", values: sort.Select(s => $"{s.Field} {s.Dir}"));
            return queryable.OrderBy(ordering);
        }

        return queryable;
    }

    // Bir filter altındaki tüm filters toplayan yardımcı metot
    public static IList<Filter> GetAllFilters(Filter filter)
    {
        // Toplanacak filters için liste oluştur
        List<Filter> filters = new();

        // filters topla
        GetFilters(filter, filters);
        return filters;
    }

    // Bir filter altındaki tüm filters toplayan recursive yardımcı metot
    private static void GetFilters(Filter filter, IList<Filter> filters)
    {
        // Ana filter listeye ekle
        filters.Add(filter);

        // Eğer filter alt filter içeriyorsa, alt filter de topla
        if (filter.Filters is not null && filter.Filters.Any())
            foreach (Filter item in filter.Filters)
                GetFilters(item, filters);
    }

    // Bir filter ifadesini dynamic bir filter ifadesine dönüştüren yardımcı metot
    public static string Transform(Filter filter, IList<Filter> filters)
    {

        // filter alanı boşsa istisna fırlat
        if (string.IsNullOrEmpty(filter.Field))
            throw new ArgumentException("Invalid Field");

        // filter operatörü boşsa veya geçerli bir operatör değilse istisna fırlat
        if (string.IsNullOrEmpty(filter.Operator) || !_operators.ContainsKey(filter.Operator))
            throw new ArgumentException("Invalid Operator");

        // filter indeksini al
        int index = filters.IndexOf(filter);

        // filter karşılaştırma ifadesini belirle
        string comparison = _operators[filter.Operator];

        // filter ifadesini oluşturacak bir StringBuilder oluştur
        StringBuilder where = new();


        // Eğer filter değeri boş değilse
        if (!string.IsNullOrEmpty(filter.Value))
        {
            // Operatör "doesnotcontain" ise özel bir durumu ele al
            if (filter.Operator == "doesnotcontain")
                where.Append($"(!np({filter.Field}).{comparison}(@{index.ToString()}))");

            // Diğer durumlarda, normal filter ifadesini oluştur
            else if (comparison is "StartsWith" or "EndsWith" or "Contains")
                where.Append($"(np({filter.Field}).{comparison}(@{index.ToString()}))");
            else
                where.Append($"np({filter.Field}) {comparison} @{index.ToString()}");
        }
        // filter değeri boşsa ve operatör "isnull" veya "isnotnull" ise
        else if (filter.Operator is "isnull" or "isnotnull")
        {
            // İlgili filter ifadesini oluştur
            where.Append($"np({filter.Field}) {comparison}");
        }

        // Eğer filter mantıksal operatörü ve alt filters varsa
        if (filter.Logic is not null && filter.Filters is not null && filter.Filters.Any())
        {
            // Geçerli mantıksal operatörü kontrol et
            if (!_logics.Contains(filter.Logic))
                throw new ArgumentException("Invalid Logic");

            // Tüm alt filters dönüştürüp birleştirerek geçerli filter ifadesini oluştur
            return $"{where} {filter.Logic} ({string.Join(separator: $" {filter.Logic} ", value: filter.Filters.Select(f => Transform(f, filters)).ToArray())})";
        }

        // Oluşturulan filter ifadesini geri döndür
        return where.ToString();
    }
}
