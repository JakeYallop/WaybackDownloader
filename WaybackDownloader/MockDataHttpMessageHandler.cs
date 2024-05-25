using System.Net;

namespace WaybackDownloader;

internal sealed class MockDataHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = request.RequestUri!.AbsolutePath switch
        {
            var x when x.StartsWith("/cdx/search/cdx", StringComparison.OrdinalIgnoreCase) => await CreateCdxResponseAsync(cancellationToken).ConfigureAwait(false),
            _ => await CreateHtmlResponseAsync(cancellationToken).ConfigureAwait(false)
        };

        return response;
    }

    private static async Task<HttpResponseMessage> CreateCdxResponseAsync(CancellationToken cancellationToken)
    {
        var a = new HttpResponseMessage(HttpStatusCode.OK);
        a.Content = new StringContent($"""
            org,archive)/about/"%20id= {GetYear()}0731030657 http://www.archive.org/about/%22%20id= text/html 302 OXCFCQFVBEUULMT2CUOBYU4T42VH7MYM 338
            org,archive)/about/"%20id= {GetYear()}0731030659 http://archive.org/about/%22%20id= text/html 404 6GBF3VJD2SDHBHKQNNV7OAWW6YW33AKO 2943
            org,archive)/about/%09identifier_marc.xml: {GetYear()}0731030709 http://www.archive.org/about/%09IDENTIFIER_marc.xml: text/html 302 OXCFCQFVBEUULMT2CUOBYU4T42VH7MYM 350
            org,archive)/about/%09identifier_marc.xml: {GetYear()}0731030710 http://archive.org/about/%09IDENTIFIER_marc.xml: text/html 404 D6CHVFU5PP4OYNILSUWTASOOQLF6XSZC 2954
            org,archive)/about/%09identifier_meta.xml {GetYear()}0731030705 http://www.archive.org/about/%09IDENTIFIER_meta.xml text/html 302 OXCFCQFVBEUULMT2CUOBYU4T42VH7MYM 349
            org,archive)/about/%09identifier_meta.xml {GetYear()}0731030706 http://archive.org/about/%09IDENTIFIER_meta.xml text/html 404 YZNHYLIJFOUQZZYIYLJRAKMT42OXT2NS 2955
            org,archive)/about/%0d%0aexclude.php {GetYear()}0726224227 http://www.archive.org/about/%0d%0aexclude.php text/html 404 RECXT54QH34IINX54NU4VRFSGJD4MFMZ 2942
            org,archive)/about/%0d%0aexclude.php {GetYear()}0906001931 http://www.archive.org/about/%0d%0aexclude.php text/html 404 CYSYSFG5DXSXMLLR25X5MNP2Z5FXLD22 2313
            org,archive)/about/%0d%0aexclude.php {GetYear()}0913011111 http://www.archive.org/about/%0d%0aexclude.php text/html 404 CYSYSFG5DXSXMLLR25X5MNP2Z5FXLD22 2313
            org,archive)/about/%0d%0aexclude.php {GetYear()}0919234953 http://www.archive.org/about/%0d%0aexclude.php text/html 404 CYSYSFG5DXSXMLLR25X5MNP2Z5FXLD22 2316
            """);

        await Task.Delay(15, cancellationToken).ConfigureAwait(false);

        return a;
    }

    //private static int _year = 1001;
    //private static int GetYear() => Interlocked.Increment(ref _year);
    private static int GetYear() => 2000;

    private static async Task<HttpResponseMessage> CreateHtmlResponseAsync(CancellationToken cancellationToken)
    {
        var a = new HttpResponseMessage(HttpStatusCode.OK);
        a.Content = new StringContent("""
            !DOCTYPE HTML
            <html>
                <body>
                <body>
            </html>
            """);

        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken).ConfigureAwait(false);

        return a;
    }
}
