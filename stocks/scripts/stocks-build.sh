# Stop currently runing service if it is runing as a service
if sudo systemctl is-active --quiet stocks.service
then sudo systemctl stop stocks.service
fi

# Remove previous version of the service file
sudo rm -f /etc/systemd/system/stocks.service

# Stop currently runing stocks.dll if it wasn't runing as a service
unset PID
PID=$(ps aux | grep '[s]tocks.dll' | awk '{print $2}')
if [ ! -z "$PID" ]
then while sudo kill $PID
do sleep 1
done
fi
unset PID

# Change to directory with stocks.csproj
cd /var/aspnetcore/stocks/stocks

# Copy new version of the service file to replace the previous on
sudo cp system/stocks.service /etc/systemd/system/

# Remove previous version of the main application directory
sudo rm -fr /var/aspnetcore/stocks-app

# Publish compile new version of application
sudo dotnet publish -o /var/aspnetcore/stocks-app -c Release

# Change directory with stocks.dll
cd /var/aspnetcore/stocks-app

# Remove source code directory
sudo rm -r /var/aspnetcore/stocks

# Reload service files
sudo systemctl daemon-reload

# Restart the application service
sudo systemctl restart stocks.service
