<p align="center">
    <h3 align="center">Cloud Design Patterns</h3>

  <p align="center">
    Yet-another-cloud-design-pattern-repo
    <br>
    (YACDPR)
</p>

Some of the `markdown` files are generated from data stored in json files.
This is primarily to avoid human error for pages that still require some HTML.
The process that converts the`build`to markdown uses a utility script located in the `build` folder.
To run the build script, navigate to the root folder of this repository.
## List of patterns
## Understanding the local build process

## Resources

# Serilog.Sinks.EventLog

A Serilog sink that writes events to the Windows Event Log.

**Important:** version 3.0 of this sink changed the default value of `manageEventSource` from `true` to `false`. `Applications` that run with administrative priviliges, and that can therefore create event sources on-the-fly, can opt-in by providing `manageEventSource: true` as a configuration option.

### Getting started

First, install the package from NuGet:

```
Install-Package Serilog.Sinks.EventLog
```

The sink is configured by calling `WriteTo.EventLog()` on the `LoggerConfiguration`:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.EventLog("Sample App", manageEventSource: true)
    .CreateLogger();

Log.Information("Hello, Windows Event Log!");

Log.CloseAndFlush();
```

Events will appear under the Application log with the specified source name:

![Screenshot](https://raw.githubusercontent.com/serilog/serilog-sinks-eventlog/dev/assets/Screenshot.png)
<!--stackedit_data:
eyJoaXN0b3J5IjpbLTExNjMxMDA2MCw2MTgwOTMzMTIsLTY1Nj
IwMTkzNV19
-->