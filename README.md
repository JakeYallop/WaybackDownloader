[![Version](https://img.shields.io/github/v/tag/JakeYallop/WaybackDownloader?sort=semver&label=Wayback%20Downloader)](https://github.com/JakeYallop/WaybackDownloader/releases)
[![Build](https://github.com/JakeYallop/WaybackDownloader/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/JakeYallop/WaybackDownloader/actions/workflows/build-and-test.yml)

## Wayback Machine Downloader

WaybackDownloader is a CLI tool for downloading the latest copy of all pages of a website from the wayback machine.

## Installation
Build from source, or download it from the [releases](https://github.com/JakeYallop/WaybackDownloader/releases) page.

## Basic Usage

To use the tool in its simplest form, use the following command:

```bash
WaybackDownloader.exe "www.example.com" "./example"
```

In this command, `www.example.com` is the website to download, and `./example` is the directory where the downloaded pages will be stored.

## Command Line Options

### History Log Directory
The Wayback Downloader uses a log to store information about webpages it has already downloaded. By default, a folder is created in the current working directory under "/downloadHistory". To specify a custom path, use the `--historyLogDir` option.
```bash
WaybackDownloader.exe <matchUrl> <outputDir> --historyLogDir ../../customHistoryLogFolder
```

### Match Type

Specify the match type using the `-m` or `--matchType` option. The default value is 'exact'. Other possible values include 'prefix', 'domain', and 'host'.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> -m Prefix
```

The `matchType` option determines how the <matchUrl> is matched against the URLs in the Wayback Machine. Using example.com as an example:

| Match Type | Description | Command |
|------------|-------------|---------|
| `exact` (default) | Returns results matching exactly `example.com` | `WaybackDownloader.exe example.com outputDir -m exact` |
| `prefix` | Returns results for all results under the path `example.com` | `WaybackDownloader.exe example.com outputDir -m prefix` |
| `host` | Returns results from host `example.com` | `WaybackDownloader.exe example.com outputDir -m host` |
| `domain` | Returns results from host `example.com` and all subhosts `*.example.com` | `WaybackDownloader.exe example.com outputDir -m domain` |

### Time Range

Define a time range using the `--from` and `--to` options. The timestamp should follow the wayback machine format `yyyyMMddHHmmss`. At least a 4-digit year must be specified when specifying a timestamp.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> --from 20200101 --to 20201231
```

### Filters

Apply filters using the `-f` or `--filters` option. The default filters are 'statuscode:200' and 'mimetype:text/html'.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> -f !statuscode:404 -f !statuscode:302
```

### Page Filters

Use the `-p` or `--pageFilters` option to apply page filters. Once a page has been downloaded, it will only be saved to disk if it contains one of the words in this list.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> -p keyword1 -p keyword2
```

### Limit Pages

Limit the number of pages processed using the `--limitPages` option. This is an absolute limit on the number of pages processed, two versions of the same page will count twice.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> --limitPages 100
```

### Rate Limit

> [!WARNING]
> Setting a high rate limit is not recommended as it can lead to throttling or temporary blacklisting by the wayback machine and archive.org.

Set the rate limit for the number of pages to download per second using the `-r` or `--rateLimit` option. The default value is 5.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> -r 10
```

### Clear History

Clear the history of previously downloaded pages using the `--clearHistory` option.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> --clearHistory
```

### Verbose

Enable verbose logging using the `-v` or `--verbose` option.

```bash
WaybackDownloader.exe <matchUrl> <outputDir> -v
```

## Advanced Example

Here's an example of a more complex use case:

```bash
WaybackDownloader.exe http://example.com ./downloads -m Prefix --from 20200101 --to 20201231 -f !statuscode:404 -p keyword1 -p keyword2 --limitPages 100 -r 10
```

This command will download pages from ‘http://example.com’, save them to the ‘./downloads’ directory, match URLs that start with ‘http://example.com’, only download pages from the year 2020, exclude pages with a 404 status code, only save pages that contain ‘keyword1’ or ‘keyword2’, process a maximum of 100 pages, and download a maximum of 10 pages per second. 
