# bnet #
bnet is a reference implementation with logging and extensibility in mind: you can add your own commands by implementing IRconCommand.
Be sure to right click the solution in Visual Studio and select "Enable NuGet package restore" before building it.

Changes to BattleNET:
* Command responses are now received throw event CommandResponseReceived, and they don't trigger MessageReceived
* Optionally you can supply your own handler for the specific command you're sending with SendCommandPacket, so you don't need to iterate through all the received responses for the one you're expecting. (Recommended)

# BattleNET #

BattleNET is a C# (.NET) library and client for the BattlEye protocol.

#### Source code content ####

```
BattleNET           - The library
BattleNET client    - The client
AUTHORS.txt         - BattleNET authors
BattleNET.sln       - BattleNET solution
CHANGELOG.txt       - Changes made to BattleNET
COPYING.txt         - The LGPL license
README.md           - This file
```

#### BattleNET client ####

The BattleNET client basically replicates the official BE RCon client but uses the BattleNET library to do all of it's work.

Usage:

```
BattleNET client.exe -host [ipaddress] -port [portnumber] -password [password] -command [command]
```
Command line options:
```
-host           [ipaddress]     RCon ip address
-port           [portnumber]    RCon port number
-password       [password]      RCon password
-command        [command]       Sends command to RCon server and exits again
Note: If no arguments are specified the client will ask for the login details.
```

Examples:

```
BattleNET client.exe -host 127.0.0.1 -port 2302 -password 123456789
BattleNET client.exe -host 127.0.0.1 -port 2302 -password 123456789 -command "say -1 Hello World!"
```

#### BattleNET library ####

Implementation sample:
https://github.com/ziellos2k/BattleNET/blob/master/BattleNET%20client/Program.cs
