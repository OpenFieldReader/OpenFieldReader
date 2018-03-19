#!/bin/bash

# To support different ubuntu version check: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
# And you must also change in demo.csproj the value for RuntimeIdentifiers

targets=( ubuntu.16.04-x64 )

rm -rf temp
cp -r ./template ./temp
cp -r ../src/*.cs ./temp
cp -r ../src/*.csproj ./temp

# Remove previous packages.
rm -rf *.deb

for i in "${targets[@]}"
do
    echo "Compiling for $i"

    rm -rf temp/usr/bin/openfieldreaderbin
    rm -rf temp/bin
    rm -rf temp/obj

    cd temp

    dotnet restore

    dotnet publish -c Release -r $i
    
    mkdir usr/bin/openfieldreaderbin
    cp -R bin/Release/netcoreapp2.0/$i/publish/* usr/bin/openfieldreaderbin

    sudo chmod 755 DEBIAN/post*
    sudo chmod 755 DEBIAN/pre*

    cd ..
    dpkg-deb --build temp
    mv temp.deb openfieldreader-$i.deb
done


echo -e "\nUpload: openfieldreader-[ubuntu version].deb"
echo -e "Make sure to install libunwind8 (apt-get install libunwind8)"
echo -e "Installation: dpkg -i openfieldreader-[ubuntu version].deb"
echo -e "Uninstallation: apt-get remove -y openfieldreader"
echo -e "Usage: openfieldreader [args]\n"