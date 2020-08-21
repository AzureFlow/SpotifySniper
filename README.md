# SpotifySniper

SpotifySniper will hunt down and snipe Spotify desktop ads and tracking services out of existence. SpotifySniper runs on very low cpu usage.

## How it works

SpotifySniper uses a [man-in-the-middle attack](https://en.wikipedia.org/wiki/Man-in-the-middle_attack) with [Titanium Web Proxy](https://github.com/justcoding121/Titanium-Web-Proxy) to intercept traffic and block requests for ads. On startup the program will ask to install a root certificate to `certmgr.msc → Trusted Root Certification Authorities → Titanium Root Certificate Authority`.

## Setup

1. Download the [latest release](https://github.com/AzureFlow/SpotifySniper/releases) and unarchive it to a directory with user access.
2. Start the program accept the request to install the certificate
3. Optionally make SpotifySniper run on startup

### Run on startup

To make SpotifySniper run on startup simply add a shortcut from `SpotifySniper.exe` to `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup`. It's a planned feature to automatically setup run on startup however it's unlikely.

## Support:

This project is provided "as is" and without support. **Warning:** Blocking Spotify ads is against the [EULA](https://www.spotify.com/us/legal/end-user-agreement/#s9) and may result in your account being terminated.

## License

SpotifySniper is licensed under the [MIT](LICENSE.txt) license.