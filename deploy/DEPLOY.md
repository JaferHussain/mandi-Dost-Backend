# Putting the Mandi Dost API on a Plesk / IIS (Windows) server

This guide matches the setup in Plesk: a Windows site, SQL Server 2022 and a database called `MandiDostDB`.
Nothing secret is stored in this repository: the passwords and keys are typed into `web.config` **on the server**.

## 0. What you need on the server

- A Windows site with **ASP.NET Core** support (the "ASP.NET Core Module" / hosting bundle), and an HTTPS certificate.
- The database `MandiDostDB` and its user (already created in Plesk > Databases).

If you are not sure the server has .NET 10, use the **self-contained** package below: it carries its own .NET and needs only the ASP.NET Core Module.

## 1. Build the package (on your PC)

```bash
cd backend/src/MandiDost.Api
# small package, needs .NET 10 on the server
dotnet publish -c Release -r win-x64 --self-contained false -o ../../publish/framework-dependent
# bigger package (about 100 MB), works even without .NET installed on the server
dotnet publish -c Release -r win-x64 --self-contained true  -o ../../publish/self-contained
```

Then copy `deploy/web.config.template` over the `web.config` in the folder you will upload. Zip the folder's contents.

## 2. Upload

1. Plesk > Websites & Domains > your site > **File Manager**.
2. Open the site's root folder (`httpdocs`), delete the default files, upload the zip and **Extract** it.
   The folder must contain `MandiDost.Api.exe`, `MandiDost.Api.dll` and `web.config` directly (not inside another folder).

## 3. Fill in `web.config` (on the server)

Open `web.config` in File Manager > Edit and replace every `PUT_..._HERE`:

| Setting | What to put |
|---|---|
| `Database__Password` | The database user's password from Plesk. (Or leave this blank and replace `Password=PUT_DATABASE_PASSWORD_HERE` in `appsettings.Production.json` on the server instead. If both are set, `web.config` wins.) |
| `Jwt__SigningKey` | A long random key, 32 or more letters and digits |
| `Seed__AdminPhone`, `Seed__AdminPassword` | Your own administrator login (phone like `03001234567`) |

Use letters and digits only: characters such as `&`, `<`, `>` and `"` break the file.
The server, database name and user are in `appsettings.Production.json` (`ConnectionStrings:Default`). If yours are different, change them there.

## 4. Site settings in Plesk

- **HTTPS:** SSL/TLS Certificates > get a free Let's Encrypt certificate for the domain, and turn on the redirect from HTTP to HTTPS. The Play Store app needs `https://`.
- **Application pool:** if Plesk lets you choose the .NET CLR version, choose **No Managed Code**.
- **Uploaded photos and voice notes** are saved in `App_Data\media`. The site's user needs permission to write there
  (Plesk normally gives it). If photo upload fails later, give that folder "Modify" permission in File Manager.

## 5. First start and test

1. Open `https://YOUR-DOMAIN/health` in a browser. After a short wait it must say **Healthy**.
   The first start creates all the tables (because `Database__MigrateOnStartup` is `true`) and your admin account.
2. Open `https://YOUR-DOMAIN/api/locations`. You should see the list of cities.
3. **Now edit `web.config` again and change `Database__MigrateOnStartup` to `false`.**
   (It only needs to be `true` for the first start and after an update that changes the database.)

## 6. Point the app at it

In `mobile/eas.json`, replace `https://REPLACE-WITH-YOUR-API-DOMAIN` with your real address
(both the `preview` and `production` profiles), then build the Play Store file.

## If it does not start

| What you see | What to do |
|---|---|
| HTTP 500.19 or 500.30 | Turn on logging: in `web.config` set `stdoutLogEnabled="true"`, create an empty `logs` folder, reload the page, and read `logs\stdout…` for the reason. Turn logging off again afterwards. |
| "ASP.NET Core Module not found" / error 500.19 about `AspNetCoreModuleV2` | The server does not have the ASP.NET Core hosting bundle. Ask SkyHost to install it. |
| "framework not found" / ".NET 10 required" | Use the **self-contained** package, or ask SkyHost to install .NET 10 hosting. |
| `Login failed for user` | The user name or password in the connection string does not match Plesk. |
| `Cannot open database` | The database name is different from `MandiDostDB`. |
| Cannot connect / "server was not found" for `.\MSSQLSERVER2022` | The named instance cannot be reached by name. Ask SkyHost for the instance's **port** and use `Server=localhost,PORT` in `appsettings.Production.json`. |
| `Jwt:SigningKey must be at least 32 bytes` | The signing key is too short or still says `PUT_...`. |

## Updating later

Upload the new files over the old ones, but **keep your `web.config` and the `App_Data` folder**.
Back up the database and the `App_Data\media` folder together. They belong together.

## Optional: create the tables with a script instead

`deploy/mandidost-database.sql` creates the same tables. Use it only if you prefer not to let the app do it:
run it against `MandiDostDB` with SQL Server Management Studio or `sqlcmd` (it needs the `GO` batch separator, which many web tools do not support),
and keep `Database__MigrateOnStartup` as `false`.
