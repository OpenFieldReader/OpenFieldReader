#!/bin/bash

# To support different ubuntu version check: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
# And you must also change in demo.csproj the value for RuntimeIdentifiers

targets=( ubuntu.14.04-x64 ubuntu.16.04-x64 ubuntu.16.10-x64 )

# Remove previous packages.
rm -rf *.deb

# Prepare output folder.
mkdir ../bin

for i in "${targets[@]}"
do
    echo "Compiling for $i"

    rm -rf temp
    cp -r ./template ./temp
    cp -r ../src/*.cs ./temp
    cp -r ../src/*.csproj ./temp

    rm -rf temp/usr/bin/openfieldreaderbin
    rm -rf temp/bin
    rm -rf temp/obj

    cd temp

    dotnet restore -r $i

    dotnet publish -c Release -r $i
    
    mkdir usr
    mkdir usr/bin
    mkdir usr/bin/openfieldreaderbin
    cp -R bin/Release/netcoreapp2.0/$i/publish/* usr/bin/openfieldreaderbin

    chmod 0775 DEBIAN/post*
    chmod 0775 DEBIAN/pre*

    chmod +x usr/bin/openfieldreader
    chmod +x usr/bin/openfieldreaderbin/OpenFieldReader

    rm -rf bin
    rm -rf obj
    rm -rf *.cs
    rm -rf *.csproj

    cd ..

    dpkg-deb --build temp
    mv temp.deb ../bin/openfieldreader-$i.deb
done


echo -e "\nUpload: openfieldreader-[ubuntu version].deb"
echo -e "Make sure to install libunwind8 (apt-get install libunwind8)"
echo -e "Installation: dpkg -i openfieldreader-[ubuntu version].deb"
echo -e "Uninstallation: apt-get remove -y openfieldreader"
echo -e "Usage: openfieldreader [args]\n"