# Tutorial 7D device enrollment operator note

Tutorial 7D uses one bearer secret per sync client. The client-generated
device ID is a public identifier; the secret proves possession. Environment
variable storage is a development compromise, not production credential
storage.

## Enroll

1. Run the client `sync` command once and copy the displayed Device ID.
2. On the server machine, run:

```powershell
dotnet run --project src/RetailLab.LabCli -- enroll-device --data-directory <server-data> --device-id <guid> --name "Till 1" --staff "Amina"
```

3. Confirm the echoed device ID and label. Copy the printed key immediately;
   the database stores only its one-way hash and cannot display it later.
4. On that client only, set `RETAILLAB_SYNC_DEVICE_KEY` to the key. Never put
   it in source control, the client database, command arguments, chat, tickets,
   screenshots, or documentation.

## Rotate or revoke

Rotate after suspected exposure, device reassignment, relevant staff-access
changes, or an operator-directed security event:

```powershell
dotnet run --project src/RetailLab.LabCli -- rotate-device --data-directory <server-data> --device-id <guid>
```

Replace the client environment value with the newly printed key. The old key
stops working immediately.

For a lost or retired device:

```powershell
dotnet run --project src/RetailLab.LabCli -- revoke-device --data-directory <server-data> --device-id <guid>
```

Revocation is retained for audit; device and receipt rows are not deleted.
