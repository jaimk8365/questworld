/// Elmish MVU state: model, messages, init and update.
module QuestWorld.State

open System
open Elmish
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.QuestEngine
open QuestWorld.Catalog

// ------------------------------------------------------------------- model

type ChildTab =
    | QuestsTab
    | ShopTab
    | ArcadeTab
    | BadgesTab

type AdminTab =
    | OverviewTab
    | ApprovalsTab
    | BuilderTab
    | SettingsTab

type QuestForm =
    { title: string
      description: string
      icon: string
      questType: QuestType
      xp: int
      coins: int
      assignThea: bool
      assignLevi: bool
      recurrence: Recurrence
      requiresApproval: bool }

let emptyForm =
    { title = ""; description = ""; icon = "⭐"
      questType = Chore; xp = 20; coins = 8
      assignThea = true; assignLevi = true
      recurrence = EveryDay; requiresApproval = true }

type Celebration =
    { outcome: CompletionOutcome
      forUser: string }

type Model =
    { data: AppData
      currentUserId: string option
      // login screen
      selectedProfile: string option
      passwordInput: string
      loginError: string option
      // navigation
      childTab: ChildTab
      adminTab: AdminTab
      // parent quest builder
      questForm: QuestForm
      builderMessage: string option
      // parent password manager
      pwTargetUser: string
      pwNewValue: string
      pwMessage: string option
      // feedback
      celebration: Celebration option
      shopMessage: string option
      // arcade (transient — never persisted)
      arcadeGame: Arcade.Game option
      arcadeResult: ArcadeRunResult option
      arcadeMessage: string option }

let currentUser (model: Model) : User option =
    model.currentUserId
    |> Option.bind (fun id -> model.data.users |> List.tryFind (fun u -> u.id = id))

// ---------------------------------------------------------------- messages

type Msg =
    | SelectProfile of string
    | BackToProfiles
    | PasswordChanged of string
    | AttemptLogin
    | Logout
    | ChildTabChanged of ChildTab
    | AdminTabChanged of AdminTab
    | CompleteQuest of string
    | DismissCelebration
    | ApproveCompletion of QuestCompletion
    | RejectCompletion of QuestCompletion
    | BuyCosmetic of string
    | ToggleEquip of string
    | FormChanged of QuestForm
    | SubmitQuest
    | ToggleQuestActive of string * bool
    | DeleteQuest of string
    | ToggleSound
    | PwTargetChanged of string
    | PwValueChanged of string
    | SubmitPassword
    | ResetAllData
    | BuyArcadeToken
    | ArcadeStart
    | ArcadeFlap
    | ArcadeTick
    | ArcadeExit

// -------------------------------------------------------------------- init

let init () : Model * Cmd<Msg> =
    let data =
        match Storage.loadData () with
        | Some d -> d
        | None ->
            let seed = seedData
            Storage.saveData seed
            seed
    let session = Storage.loadSession ()
    let userId =
        session
        |> Option.map (fun s -> s.userId)
        |> Option.filter (fun id -> data.users |> List.exists (fun u -> u.id = id))
    { data = data
      currentUserId = userId
      selectedProfile = None
      passwordInput = ""
      loginError = None
      childTab = QuestsTab
      adminTab = OverviewTab
      questForm = emptyForm
      builderMessage = None
      pwTargetUser = "u-thea"
      pwNewValue = ""
      pwMessage = None
      celebration = None
      shopMessage = None
      arcadeGame = None
      arcadeResult = None
      arcadeMessage = None },
    Cmd.none

// ------------------------------------------------------------------ update

let private rng = Random()

let private persist (data: AppData) : Cmd<Msg> =
    Cmd.ofEffect (fun _ -> Storage.saveData data)

let private playFor (model: Model) (sound: unit -> unit) =
    if model.data.settings.soundOn then sound ()

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // ------------------------------------------------------------- login
    | SelectProfile id ->
        playFor model Sounds.tap
        { model with selectedProfile = Some id; passwordInput = ""; loginError = None }, Cmd.none

    | BackToProfiles ->
        { model with selectedProfile = None; passwordInput = ""; loginError = None }, Cmd.none

    | PasswordChanged value ->
        { model with passwordInput = value; loginError = None }, Cmd.none

    | AttemptLogin ->
        match model.selectedProfile |> Option.bind (fun id -> model.data.users |> List.tryFind (fun u -> u.id = id)) with
        | None -> model, Cmd.none
        | Some user ->
            match Auth.login model.data.users user.username model.passwordInput with
            | Error e ->
                playFor model Sounds.oops
                { model with loginError = Some e }, Cmd.none
            | Ok user ->
                playFor model Sounds.questComplete
                let session = { userId = user.id; loggedInAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") }
                { model with
                    currentUserId = Some user.id
                    selectedProfile = None
                    passwordInput = ""
                    loginError = None
                    childTab = QuestsTab
                    adminTab = OverviewTab },
                Cmd.ofEffect (fun _ -> Storage.saveSession (Some session))

    | Logout ->
        { model with currentUserId = None; selectedProfile = None; celebration = None
                     arcadeGame = None; arcadeResult = None },
        Cmd.ofEffect (fun _ -> Storage.saveSession None)

    // -------------------------------------------------------- navigation
    | ChildTabChanged tab ->
        playFor model Sounds.tap
        { model with childTab = tab; shopMessage = None
                     arcadeGame = None; arcadeResult = None; arcadeMessage = None }, Cmd.none

    | AdminTabChanged tab ->
        { model with adminTab = tab; builderMessage = None; pwMessage = None }, Cmd.none

    // ------------------------------------------------------ quest flow
    | CompleteQuest questId ->
        match model.currentUserId with
        | None -> model, Cmd.none
        | Some userId ->
            let data', outcome = markDone model.data userId questId DateTime.Now rng
            match outcome with
            | None -> model, Cmd.none
            | Some o ->
                if o.pendingApproval then playFor model Sounds.tap
                elif o.levelAfter > o.levelBefore then playFor model Sounds.levelUp
                else playFor model Sounds.questComplete
                { model with data = data'; celebration = Some { outcome = o; forUser = userId } },
                persist data'

    | DismissCelebration ->
        { model with celebration = None }, Cmd.none

    | ApproveCompletion completion ->
        let data', _ = approve model.data completion rng
        { model with data = data' }, persist data'

    | RejectCompletion completion ->
        let data' = reject model.data completion
        { model with data = data' }, persist data'

    // -------------------------------------------------------------- shop
    | BuyCosmetic cosmeticId ->
        match model.currentUserId with
        | None -> model, Cmd.none
        | Some userId ->
            match buyCosmetic model.data userId cosmeticId with
            | Error e ->
                playFor model Sounds.oops
                { model with shopMessage = Some e }, Cmd.none
            | Ok data' ->
                playFor model Sounds.coin
                { model with data = data'; shopMessage = Some "Unlocked! It's yours! 🎉" }, persist data'

    | ToggleEquip cosmeticId ->
        match model.currentUserId with
        | None -> model, Cmd.none
        | Some userId ->
            playFor model Sounds.tap
            let data' = toggleEquip model.data userId cosmeticId
            { model with data = data' }, persist data'

    // ----------------------------------------------------- quest builder
    | FormChanged form ->
        { model with questForm = form; builderMessage = None }, Cmd.none

    | SubmitQuest ->
        let f = model.questForm
        let assignees =
            [ if f.assignThea then "u-thea"
              if f.assignLevi then "u-levi" ]
        if f.title.Trim() = "" then
            { model with builderMessage = Some "Give the quest a title." }, Cmd.none
        elif List.isEmpty assignees then
            { model with builderMessage = Some "Assign the quest to at least one child." }, Cmd.none
        else
            let quest =
                { id = "q-" + string (DateTime.Now.Ticks)
                  title = f.title.Trim()
                  description = f.description.Trim()
                  icon = (if f.icon.Trim() = "" then "⭐" else f.icon.Trim())
                  questType = f.questType
                  reward = { xp = max 1 f.xp; coins = max 0 f.coins }
                  assignedTo = assignees
                  recurrence = f.recurrence
                  requiresApproval = f.requiresApproval
                  active = true }
            let data' = addQuest model.data quest
            { model with data = data'; questForm = emptyForm; builderMessage = Some (sprintf "Quest “%s” created! ✅" quest.title) },
            persist data'

    | ToggleQuestActive (questId, active) ->
        let data' = setQuestActive model.data questId active
        { model with data = data' }, persist data'

    | DeleteQuest questId ->
        let data' = deleteQuest model.data questId
        { model with data = data' }, persist data'

    // ----------------------------------------------------------- settings
    | ToggleSound ->
        let data' = { model.data with settings = { model.data.settings with soundOn = not model.data.settings.soundOn } }
        { model with data = data' }, persist data'

    | PwTargetChanged id ->
        { model with pwTargetUser = id; pwMessage = None }, Cmd.none

    | PwValueChanged v ->
        { model with pwNewValue = v; pwMessage = None }, Cmd.none

    | SubmitPassword ->
        match Auth.changePassword model.data model.pwTargetUser model.pwNewValue with
        | Error e -> { model with pwMessage = Some e }, Cmd.none
        | Ok data' ->
            { model with data = data'; pwNewValue = ""; pwMessage = Some "Password updated ✅" },
            persist data'

    | ResetAllData ->
        let seed = seedData
        { model with data = seed; currentUserId = None; selectedProfile = None },
        Cmd.batch [ persist seed; Cmd.ofEffect (fun _ -> Storage.saveSession None) ]

    // ------------------------------------------------------------ arcade
    | BuyArcadeToken ->
        match model.currentUserId with
        | None -> model, Cmd.none
        | Some userId ->
            match buyArcadeToken model.data userId with
            | Error e ->
                playFor model Sounds.oops
                { model with arcadeMessage = Some e }, Cmd.none
            | Ok data' ->
                playFor model Sounds.coin
                { model with data = data'; arcadeMessage = Some "Token bought! 🎟️" }, persist data'

    | ArcadeStart ->
        match model.currentUserId with
        | None -> model, Cmd.none
        | Some userId ->
            match spendArcadeToken model.data userId with
            | None ->
                playFor model Sounds.oops
                { model with arcadeMessage = Some "You need a token — 10 🪙 in the booth!" }, Cmd.none
            | Some data' ->
                playFor model Sounds.tap
                { model with
                    data = data'
                    arcadeGame = Some (Arcade.newGame rng)
                    arcadeResult = None
                    arcadeMessage = None },
                persist data'

    | ArcadeFlap ->
        { model with arcadeGame = model.arcadeGame |> Option.map Arcade.flap }, Cmd.none

    | ArcadeTick ->
        match model.arcadeGame, model.currentUserId with
        | Some game, Some userId when game.phase = Arcade.Flying ->
            let game' = Arcade.step rng game
            if game'.phase = Arcade.GameOver then
                let data', result = finishArcadeRun model.data userId game'.score game'.stars
                if result.newBest then playFor model Sounds.levelUp
                elif result.coinsEarned > 0 then playFor model Sounds.coin
                { model with data = data'; arcadeGame = Some game'; arcadeResult = Some result },
                persist data'
            else
                { model with arcadeGame = Some game' }, Cmd.none
        | _ -> model, Cmd.none

    | ArcadeExit ->
        { model with arcadeGame = None; arcadeResult = None }, Cmd.none

/// Ticks the arcade at ~30fps, but only while a run is in flight.
let subscribe (model: Model) : Sub<Msg> =
    match model.arcadeGame with
    | Some game when game.phase = Arcade.Flying ->
        [ [ "arcade-tick" ],
          fun dispatch ->
              let id = Browser.Dom.window.setInterval ((fun () -> dispatch ArcadeTick), 33)
              { new System.IDisposable with
                  member _.Dispose() = Browser.Dom.window.clearInterval id } ]
    | _ -> []
