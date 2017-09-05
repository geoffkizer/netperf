@echo off

dotnet run -c release -- client -s 1 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 16 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 256 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 4096 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 65536 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 1048576 -i 10 -t 60 -e %1

dotnet run -c release -- client --ssl -s 1 -i 10 -t 60 -e %2
dotnet run -c release -- client --ssl -s 16 -i 10 -t 60 -e %2
dotnet run -c release -- client --ssl -s 256 -i 10 -t 60 -e %2
dotnet run -c release -- client --ssl -s 4096 -i 10 -t 60 -e %2
dotnet run -c release -- client --ssl -s 65536 -i 10 -t 60 -e %2
dotnet run -c release -- client --ssl -s 1048576 -i 10 -t 60 -e %2



