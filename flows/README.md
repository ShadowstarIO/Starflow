# Shared flows

Flows to bring into Starflow. Some are mine, some come from other players.

## Use one

**In game:** open `/starflow`, click **Templates**, then **Shared flows from GitHub...**. Pick one to see what it does, then click **Add**. Flows come in off; tick **On** once you've read it through.

**By hand:** open a `.json` file here, click **Copy raw file**, then in Starflow click **Import** and paste.

Check what a flow does before turning it on, especially Chat cards and anything that sends commands.

## Share one

1. In Starflow, select the flow and click **Copy JSON**.
2. Save it here as `short-name.json`: one flow per file, or a few related flows as a list.
3. Put a line or two in the flow's **Notes**: what it does and which plugins it needs.
4. Add an entry to `index.json` so it shows up in game:
   ```json
   { "File": "short-name.json", "Name": "Short name", "Description": "One line on what it does.", "Needs": ["Glamourer"], "Author": "Your name" }
   ```
5. Open a pull request, or post it in an issue and I can add it.

Before sharing, take out anything personal: character names, linkshell or FC names, webhook URLs, and private addresses.

## Flows

| File | What it does | Needs |
|---|---|---|
| [away-in-the-inn.json](away-in-the-inn.json) | Away status while you're in an inn room, back to online when you leave. | |
