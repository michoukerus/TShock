$current_dir = $ExecutionContext.SessionState.Path.CurrentLocation
function JoinPath { return [System.IO.Path]::Combine($current_dir, $args[0], $args[1], $args[2], $args[3], $args[4], $args[5]) }

$release_dir = JoinPath "releases"
$release_plugin_dir = JoinPath  "releases" "ServerPlugins"
$release_zip = JoinPath "tshock_cn_release.zip"
$debug_zip = JoinPath "tshock_cn_debug.zip"

$tsapi_bin_name = "TerrariaServer.exe"
$otapi_bin_name = "OTAPI.dll"
$mysql_bin_name = "MySql.Data.dll"
$sqlite_dep_name = "sqlite3.dll"
$sqlite_bin_name = "Mono.Data.Sqlite.dll"
$json_bin_name = "Newtonsoft.Json.dll"
$http_bin_name = "HttpServer.dll"
$tshock_bin_name = "TShockAPI.dll"
$tshock_symbols = "TShockAPI.pdb"
$bcrypt_bin_name = "BCrypt.Net.dll"
$geoip_db_name = "GeoIP.dat"
$start_cmd_script_name = "StartServer.cmd"
$start_sh_script_name = "StartServer.sh"

$tsapi_release_bin = JoinPath "TerrariaServerAPI" "TerrariaServerAPI" "bin" "Release" $tsapi_bin_name
$tsapi_debug_bin = JoinPath "TerrariaServerAPI" "TerrariaServerAPI" "bin" "Debug" $tsapi_bin_name
$otapi_bin = JoinPath "TerrariaServerAPI" "TerrariaServerAPI" "bin" "Release" $otapi_bin_name
$mysql_bin = JoinPath "packages" "MySql.Data.6.9.8" "lib" "net45" $mysql_bin_name
$sqlite_dep = JoinPath "prebuilts" $sqlite_dep_name
$sqlite_bin = JoinPath "prebuilts" $sqlite_bin_name
$http_bin = JoinPath "prebuilts" $http_bin_name
$json_bin = JoinPath "packages" "Newtonsoft.Json.9.0.1" "lib" "net45" $json_bin_name
$bcrypt_bin = JoinPath "packages" "BCrypt.Net.0.1.0" "lib" "net35" $bcrypt_bin_name
$geoip_db = JoinPath "prebuilts" $geoip_db_name
$release_bin = JoinPath "TShockAPI" "bin" "Release" $tshock_bin_name
$start_cmd_script_bin = JoinPath "scripts" $start_cmd_script_name
$start_sh_script_bin = JoinPath "scripts" $start_sh_script_name

$debug_folder = JoinPath "TShockAPI" "bin" "Debug"

function RunBootstrapper
{
    foreach ($config in "Debug", "Release")
    {
        msbuild ./TerrariaServerAPI/TShock.4.OTAPI.sln "/p:Configuration=$config"

        Set-Location "./TerrariaServerAPI/TShock.Modifications.Bootstrapper/bin/$config/"
        ./TShock.Modifications.Bootstrapper --in=OTAPI.dll --mod="../../../TShock.Modifications.**/bin/$config/TShock.Modifications.*.dll" --o=Output/OTAPI.dll

        Set-Location $current_dir
        msbuild ./TerrariaServerAPI/TerrariaServerAPI/TerrariaServerAPI.csproj "/p:Configuration=$config"
    }
}

function CopyDependencies
{
    Copy-Item $json_bin $release_dir
    Copy-Item $sqlite_dep $release_dir
    Copy-Item $geoip_db $release_dir

    Copy-Item $start_cmd_script_bin $release_dir
    Copy-Item $start_sh_script_bin $release_dir

    Copy-Item $bcrypt_bin $release_plugin_dir
    Copy-Item $mysql_bin $release_plugin_dir
    Copy-Item $sqlite_bin $release_plugin_dir
    Copy-Item $http_bin $release_plugin_dir
}

function CopyReleaseFiles
{
    Copy-Item $tsapi_release_bin $release_dir
    Copy-Item $otapi_bin $release_dir
    Copy-Item $release_bin $release_plugin_dir
}

function CopyDebugFiles
{
    Copy-Item -Path (JoinPath $debug_folder $tshock_bin_name) -Destination $release_plugin_dir
    Copy-Item -Path (JoinPath $debug_folder $tshock_symbols) -Destination $release_plugin_dir

    Copy-Item -Path $tsapi_debug_bin -Destination $release_dir
}

mkdir -Path $release_plugin_dir

git submodule update --init --recursive
appveyor-retry nuget restore TShock.sln
appveyor-retry nuget restore TerrariaServerAPI/

RunBootstrapper
CopyDependencies

msbuild .\TShock.sln /p:Configuration=Release
CopyReleaseFiles

Add-Type -Assembly System.IO.Compression.FileSystem
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
[System.IO.Compression.ZipFile]::CreateFromDirectory($release_dir, $release_zip, $compressionLevel, $false)

msbuild .\TShock.sln /p:Configuration=Debug
CopyDebugFiles

[System.IO.Compression.ZipFile]::CreateFromDirectory($release_dir, $debug_zip, $compressionLevel, $false)