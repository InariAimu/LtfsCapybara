
using System;
using System.Threading;
using Ltfs;
using TapeDrive;
using Test;

Ltfs.Ltfs lt = new();

// Initialize the test console logger. Adjust level as desired.
Log.SetLogger(new ConsoleLogger { Level = LogLevel.Info });

await FormatAndWrite.Test(lt);
// await WriteTestErrorRate.Test(lt);
// await Verify.Test(lt);

Console.ReadKey();
