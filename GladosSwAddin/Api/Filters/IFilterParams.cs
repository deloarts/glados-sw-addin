using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    public interface IFilterParams
    {
        IEnumerable<KeyValuePair<string, object>> ToQueryParameters();
    }
}
