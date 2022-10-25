# Beachcomber
This is a Dalamud plugin that functions as a solver for the Island Sanctuary Workshop. It takes the supply/demand chart, your island rank, and the contents of your isleventory to recommend the best workshop schedule to make the most cowries.

To use it, use /workshop to open up the menu. If you haven't viewed the Supply/Demand screen from the Tactful Taskmaster, the plugin should prompt you to do so. Once that's imported, you can hit "Run Solver" and it'll generate at least the next day's worth of schedules. Then, you select the radio button next to the schedule you want to make and it'll add its value to the total cowries at the top and generate a list of materials you need.

If you haven't opened your Isleventory, the plugin should prompt you to do so in order to compare the materials required against the materials you have. Green means you have the requisite amount of materials, yellow means you have most of them, and red means you have less than half of what you need.

You can also hit the config button to customize how the solver works like increasing the number of suggestions it makes or asking it to only show you schedules you have the rare materials for. I did my best to make helpful tooltips and instructions so that, fingers crossed, using it is fairly self-explanatory.

Ideally, you'd use it starting on Tuesday to get Wednesday's schedule, then logging on Wednesday to get Thursday's schedule, etc., but it will do its best to work from partial data later in the week and should still give good suggestions at least for the next day.
