/// Login + password hashing. Pure F# so it runs identically in the
/// browser (Fable) and in the .NET test suite.
///
/// Hashing is salted, iterated FNV-1a — deliberately dependency-free and
/// deterministic across runtimes. Appropriate for a family chore app on
/// home devices; NOT a substitute for real KDFs in a public product.
module QuestWorld.Auth

open QuestWorld.Domain

let private fnv1a64 (input: string) : uint64 =
    let mutable h = 14695981039346656037UL
    for c in input do
        h <- h ^^^ uint64 (uint32 c)
        h <- h * 1099511628211UL
    h

let hashPassword (username: string) (password: string) : string =
    let salt = "questworld::" + username.ToLowerInvariant()
    let mutable acc = salt + "::" + password
    for _ in 1 .. 500 do
        acc <- string (fnv1a64 acc) + "::" + password
    string (fnv1a64 acc)

let verifyPassword (user: User) (password: string) : bool =
    hashPassword user.username password = user.passwordHash

let findUser (users: User list) (username: string) : User option =
    let uname = username.Trim().ToLowerInvariant()
    users |> List.tryFind (fun u -> u.username.ToLowerInvariant() = uname)

let login (users: User list) (username: string) (password: string) : Result<User, string> =
    match findUser users username with
    | None -> Error "Hmm, that explorer isn't registered here."
    | Some user ->
        if verifyPassword user password then Ok user
        else Error "That secret word isn't right — try again!"

let changePassword (data: AppData) (userId: string) (newPassword: string) : Result<AppData, string> =
    if newPassword.Trim().Length < 3 then
        Error "Password must be at least 3 characters."
    else
        match data.users |> List.tryFind (fun u -> u.id = userId) with
        | None -> Error "User not found."
        | Some user ->
            let updated = { user with passwordHash = hashPassword user.username (newPassword.Trim()) }
            Ok { data with users = data.users |> List.map (fun u -> if u.id = userId then updated else u) }
