# FreeCompanyRegister

- Run FreeCompanyRegister executable in console
- Search your Free Company, by name or Lodestone ID
  - The Lodestone ID appear in your Free Company page adress : https://fr.finalfantasyxiv.com/lodestone/freecompany/{YOUR_ID}/
- It will generate a CSV file with the job levels of all the Free Company members
- Import it in Excel, Google Sheet, and more !

# Command line arguments

```
FreeCompanyRegister.exe "My Company Name or ID" /language:eu
```

- (optional) First unnamed argument is used as query, can be the Free Company name or its Lodestone ID.
- (optional) `/language:eu`: language of the Lodestone website (jp, na, eu [default], fr, de)
