@echo off

dotnet run -c release -- client -s 1 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 128 -i 10 -t 60 -e %1
dotnet run -c release -- client -s 2048 -i 10 -t 60 -e %1




