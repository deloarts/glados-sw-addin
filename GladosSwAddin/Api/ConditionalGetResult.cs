namespace GladosSwAddin.Api
{
    /// <summary>Result of GladosApiClient.GetConditionalAsync: either the parsed body plus the
    /// response's ETag header (a normal 200), or NotModified when the server answered 304
    /// against the If-None-Match this call sent — the caller keeps whatever it already has.
    /// Value is default(T) when NotModified is true.</summary>
    public struct ConditionalGetResult<T>
    {
        public bool NotModified { get; }

        public T Value { get; }

        public string ETag { get; }

        private ConditionalGetResult(bool notModified, T value, string etag)
        {
            NotModified = notModified;
            Value = value;
            ETag = etag;
        }

        public static ConditionalGetResult<T> Modified(T value, string etag) =>
            new ConditionalGetResult<T>(false, value, etag);

        public static ConditionalGetResult<T> NotModifiedResult(string etag) =>
            new ConditionalGetResult<T>(true, default(T), etag);
    }
}
