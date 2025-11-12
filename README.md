# AutoMafiles By Esicuwa

[![English](https://img.shields.io/badge/Language-English-blue)](README.md)
[![Русский](https://img.shields.io/badge/Язык-Русский-red)](README_RU.md)

The program automatically for mass management of mobile authenticators for Steam accounts.

## Description

The program automatically binds and removed Steam mobile authenticator to accounts, processing confirmation emails and creating `.maFile` files for use in SDA (Steam Desktop Authenticator) or other compatible applications.

## Features

- ✅ Automatic Steam mobile authenticator binding
- ✅ Support for various account formats (standard, with IMAP password, Outlook)
- ✅ Working through proxy servers
- ✅ Multi-threaded account processing
- ✅ Automatic confirmation code retrieval from email
- ✅ Creating `.maFile` files for each account
- ✅ Detailed process logging
- ✅ Removing .maFile from account

## System Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Internet access
- Residential Proxy servers

## Installation

1. Download the latest version of the program from the [Releases](../../releases) section
2. Extract the archive to any folder
3. Make sure .NET 8.0 Runtime is installed

## Configuration

### 1. Creating configuration file

Configuration is done through the `config.json` file in the program folder:

```json
{
    "Type": 0,
    "Threads": 3,
    "Attempts": 3,
    "Imap": "imap.firstmail.ltd",
    "Format": 0,
    "Proxy_Path": "C:\\path\\to\\proxies.txt",
    "Accounts_Path": "C:\\path\\to\\accounts.txt",
    "MaFile_Path": "C:\\path\\to\\maFiles",
	"Timeout": 1
}
```

### 2. Configuration parameters

- **Type** — type of usage (0 for binding) (1 for unbinding) of mafiles
- **Threads** - number of concurrent threads (recommended 1-5)
- **Attempts** - number of attempts for each account
- **Imap** - IMAP server for receiving emails
- **Format** - account format (0-3, see "Account Formats" section)
- **Proxy_Path** - path to proxy servers file
- **Accounts_Path** - path to accounts file
- **MaFile_Path** - path to Mafile fouder
- **Timeout** - maximum time to receive a letter in minutes


To disable mafiles, you will need maFile_Path with the path to mafile, Accounts_Path with the log:pass format of Steam accounts, and Proxy_Path, if you specify type 1.

After unlinking, the mobile authenticator will be removed and an email authenticator will be installed.

### 3. File preparation

#### Proxy servers file (`proxies.txt`)
```
user:pass@host:port
user2:pass2@host2:port2
```

**Where to get cheap and high-quality proxy servers:** [The Node Proxy Bot](https://t.me/node_proxy_bot?start=84411615) - Telegram bot for purchasing cheap and high-quality residential proxy servers, the price for 1 GB starts from $0.75.

#### Accounts file (`accounts.txt`)
Format depends on the selected type (see "Account Formats" section)

## Account Formats

### Format 0 - Standard
```
login:password:email:email_password
```

### Format 1 - With IMAP password
```
login:password:email:email_password:imap_password
```

### Format 2 - Outlook
```
login:password:email:email_password:refresh_token:client_id
```

### Format 3 - Outlook without password
```
login:password:email:refresh_token:client_id
```

## Running the program

1. Make sure all files are configured correctly
2. Run `AutoMafiles_By_Esicuwa.exe`
3. The program will start processing accounts

## Results

The program creates a folder `result/MafileAdd/[timestamp]/` with the following files:

- **result.txt** - successfully processed accounts with unbinding codes
- **error.txt** - accounts with errors and error codes
- **maFiles/** - folder with `.maFile` files for each account

## Error Codes

- **1001** - Invalid proxy format
- **1002** - Invalid account format
- **1003** - IMAP connection error
- **1004** - Account login error
- **2001** - Authentication verification error
- **2002** - Error adding authenticator
- **2003** - Phone number input required
- **2004** - Failed to add phone number
- **2005** - Error adding authenticator
- **2006** - Authenticator already bound
- **3001** - Invalid binding code
- **3002** - Failed to generate correct codes
- **3003** - Error completing binding
- **3004** - Failed to save authenticator file
- **3005** - Steam servers connection error

## Using created .maFile

Created `.maFile` files can be used in:
- Steam Desktop Authenticator
- Other compatible applications

## Security

⚠️ **IMPORTANT**: 
- For errors not related to accounts - contact the developer

## Support

If you encounter problems:
1. Check the configuration settings
2. Make sure the account and proxy formats are correct
3. Check IMAP server availability
4. Make sure proxy servers are working

## License

See [LICENSE.txt](LICENSE.txt) file

## Author

esicuwa

special thanks to the channel [svinofermacs2](https://t.me/svinofermacs2) for promoting the repository


## Donate

**TRX:** TH6SYMSiavPKFc8HBD6Kr3KtXwrCeaFgaj

**BNB:** 0x6f29ADdc4045B96ea249f6214e3b77CB923B373d
