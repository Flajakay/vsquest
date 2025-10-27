# VS Quest

It is a fork of the original mod with the addition of functionality required by the alegacy.online server. But, of course, you can use it as well.

Mod aims to add Quests to Vintage Story.<br>
It should also enable you to easily add your own quests to the game as well as your own questgivers.<br>
<br>
If you want to use this as a base for creating your own quests, please have a look at this **[example](example)**. The most important aspects to take care of are the **[quests.json](example/assets/vsquestexample/config/quests.json)** as well as the **[questgiver behavior](example/assets/vsquestexample/entities/questgiver.json#L229-L235)**<br><br>
Every quest in the config/quests/*.json can have the following attributes:
* **id**: Unique id to identify your quest in the system
* **cooldown**: cooldown in days until the questgiver offers the quest again
* **predecessor**: optional -> questid that has to be completed before this quest becomes available
* **perPlayer**: determines if the quest cooldown is set per player or globally
* **onAcceptedActions**: list of actions that are executed after the quest was accepted
  * **id**: unique id of the action
  * **args**: arguments for the function called by the action, all supplied as strings
* **gatherObjectives**: list of items the player has to offer
  * **validCodes**: list of accepted item codes
  * **demand**: needed amount
* **actionObjectives**: list of objectives that rely on custom code
  * **id**: unique id of the action objective to check
  * **args**: arguments for the function called by the action objective, all supplied as strings
* **killObjectives**: list of entities the player has to defeat
  * **validCodes**: list of accepted entity codes
  * **demand**: needed amount
* **blockPlaceObjectives**: list of blocks the player has place
  * **validCodes**: list of accepted block codes
  * **demand**: needed amount
  * **positions**: (optional) list of coordinates where the blocks have to be placed. Each coordinate is an array of 3 integers (x, y, z).
  * **removeAfterFinished**: (optional) if set to true, the placed blocks will be removed after the quest is completed.
* **blockBreakObjectives**: list of blocks the player has break
  * **validCodes**: list of accepted block codes
  * **demand**: needed amount
* **itemRewards**: list of items the player receives upon completing the quest
  * **itemCode**: code of the reward
  * **amount**: amount the player receives
* **randomItemRewards**: if you want to reward the player with a random reward (something like "select 3 out of 7 possible items") this is the place to go
  * **selectAmount**: specifies how many of the item entries should be randomly selected
  * **items**: list of items to randomize from
    * **itemCode**: code of the reward
    * **minAmount**: minimum amount of that item to drop
    * **maxAmount**: maximum amount of that item to drop
* **actionRewards**: list of rewards that rely on custom code, like spawning a certain creature, ...
  * **id**: unique id of the action
  * **args**: arguments for the function called by the action, all supplied as strings
  * **currently available actions (can be used both as actionRewards and onAcceptedActions)**:
    * despawnquestgiver: despawns the questgiver after the given amount of time
      * args: ["8"] => questgiver is despawned after 8 seconds
    * playsound: plays the given sound at the players position (only hearable by the player himself)
      * args: ["game:sounds/voice/saxophone"] => plays the saxophone sound
    * spawnentities: spawns all entities provided
      * args: ["game:wolf-male", "game:wolf-female"] => spawns a male and a female wolf
    * spawnany: spawns a random entity
      * args: ["game:wolf-male", "game:wolf-female"] => spawns either a male or a female wolf
    * recruitentity: recruits the questgiver (requires custom aitasks and is used by vsvillage)
      * args: none
    * addplayerattribute: adds an attribute as string to the watched attributes of the player, useful for storing custom data
      * args: ["isacoolguy","yes"] => sets the isacoolguy attribute of the player to yes
    * removeplayerattribute: remove a playerattribute
      * args: ["isacoolguy"] => deletes the isacoolguy attribute
    * completequest => completes the given quest
      * args: ["vsquestexample:talktome", "25"] => completes the quest vsquestexample:talktome where the questgivers entity id is 25
    * acceptquest: adds a quest to the active quests of the player
      * args: ["vsquestexample:talktome", "25"] => adds vsquestexample:talktome with questgiver 25 to the active quests of the player
    * giveitem: gives an item to the player
      * args: ["game:gear-rusty", "1"] => gives 1 rusty gear to the player
    * spawnsmoke: spawns smoke particles at the questgivers location
      * args: [] => none
    * addtraits: adds the given list of traits to the player
      * args: ["bowyer", "precise"] => adds the precise and the bowyer trait to the player
    * removetraits: removes the given list of traits from the player, but can not remove traits that are linked to the players class (eg. can not remove bowyer from hunter)
      * args: ["bowyer", "precise"] => removes the precise and the bowyer trait from the player
    * servercommand: executes a server command from the console.
      * args: ["say", "hello"] => executes "/say hello" from the server console.
    * playercommand: executes a command from the player's perspective.
      * args: ["emote", "wave"] => executes "/emote wave" as the player.
    * addjournalentry: adds a new entry to the player's journal.
      * args: ["loreCode", "title", "chapter1", "chapter2", ...] => creates a journal entry with the given lore code, title, and chapters.
    * giveactionitem: gives a player an action item defined in itemconfig.json.
      * args: ["itemId"] => gives the player the action item with the specified ID.

### Action Items

Action items are special items that can trigger a series of actions when used. They are defined in itemconfig.json

Each action item has the following properties:
* **id**: a unique id for the action item.
* **itemCode**: the code of the base item.
* **name**: the name of the action item.
* **description**: the description of the action item.
* **actions**: a list of actions to be executed when the item is used. Each action has an `id` and `args`, just like in quests.


To give an action item to a player, you can use the `giveactionitem` action in a quest or the `/giveactionitem` command.

To convert an entity to a questgiver it needs the questgiver behavior:
* **quests**: list of quests the questgiver offers
* **selectrandom**: if set to true, the questgiver will only offer a random selection of its quests
* **selectrandomcount**: determines the number of random quests the questgiver offers

![Thumbnail](resources/modicon.png)