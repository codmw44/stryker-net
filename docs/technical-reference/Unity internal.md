# Unity support internals

## Objective
This document captures knowledge gathered while working on Stryker regarding Unity related issues. It exists to assist anyone trying to analyse testing problems.
It provides architecture and design description, as well as known limitations and bugs (including github issue number when relevant).

## Unity difference in compare with .NET
Despite of Unity Developers is writing a code in C#, it's not the same as .NET. Unity is not using .NET ecosystem as .NET developers might expect.
As example,
- Standard Unity specific .gitignore includes .csproj an .sln files because they generates by Unity on opening
  - Yes, that's the reason why we have to open Unity first and only after check sln/csproj files to mutate
- Unity has own implementation of csproj files (.asmdef) with custom settings and references way
- Unity has custom TestRunner based on NUnit but with some custom features and with many limitations
    - Due to the specific nature of the game and the Unity architecture, concurrent testing is not a feature that is currently supported.
- Unity build it's code in .dll at NetFramework 4.7.1

As result of this we have to made custom UnityTestRunner to support Unity.

## Architecture

### Mutations
We use the same code for mutants generation as for .NET because it's still a .NET code.
Only special case is that we need to open Unity first and only after check sln/csproj files to mutate.

### Stryker.UnitySDK
Next we should run tests in Unity. For this we use CLI command in 'batchmode' without opening GUI. It's good method for us but every unity opening has large overhead (2 minutes for empty project for each run).
    Because of this overhead I introduced a `Stryker.UnitySDK` package which adds automatically before Unity opening

Stryker.UnitySDK is responsible for:
- Run EditMode/PlayMode tests
- Save TestResults
- Reload Unity Domain
- Close Unity
- Modify csproj files to support Stryker to mutate

#### Reload Unity Domain
Reload Unity Domain is required step to apply any changes in .dll or code to the Unity. And this is pretty heavy operation (around 5-10 sec for empty project and up to 30-45 sec for large one)
THe main issue with this it's that reload domain is part of run playmode tests. And it's very impact on test performance.

#### Commands protocol
To communicate with Unity we use custom protocol. It's based on writing/reading .txt file with custom format. `StrykerOutputDirectory/UnityListens.txt`

### Activate certain mutant
For optimization purpose we don't restart Unity for every mutant. And changing Environment variable after start the process will not affect it. As result I created a .txt files which are listening by mutants to check their activation

### Optimizations
To not run all tests for each Mutant we use two filtration stages

#### Filtration by mutanted assembly

   Stryker run only tests project which referenced directly or indirectly assembly which was mutated


#### Filtration by test modes
   
   Stryker run only test modes of test project. Which decrease overhead on empty run of playmode tests

## Initialization process

- Detect Unity Project
- Add Stryker.UnitySDK package to Unity Project
- Open Unity Project
- Find generated .sln file
- Analyze .csproj files
- Run all tests
- Mutates .dll files
- Run mutants one by one
- Report stats
- Rollback all mutants

## Faced issues

### Not support timeout tests
`Unity Test Framework` doesn't support timeout as test result. And due to Unity single thread architecture it cannot be detected for EditMode tests. Because if test go to infinite loop it will never be finished or go control to other code to catch it, which leads to not responding application or using dozens of GB of memory.

### Infinitive growing memory usage due to infinitive loop because of mutant
Some mutants can lead to infinite loop. And this leads to infinitive growing memory usage. From outside it looks like too long execution time for mutant but in fact it will never complete. Only solution here is to detect this situation and kill the Unity.
To determine memory limit you can use `--unity-memory-limit` option

### Not responding application
If application is not responding or was closed/killed by whatever reason during tests then stryker will treat it as failed tests and restart the Unity with next Mutant to run

### Detecting test assemblies
Basically we can detect test assemblies only by .asmdef files and their references. Fun part is how to detect EditMode vs PlayMode tests. It's by checking `"inclidePlatform": ["Editor"]` in .asmdef file.


## Known limitations

### Not support code coverage
Stryker use kind of callbacks from VsTest which allow to get code coverage stats during run. But Unity Test Runner doesn't support it this way
And this system have to be reimplemented for Unity by Using Stryker.UnitySDK and callbacks from it

### Not support concurrency
We cannot run tests out If Unity Test Runner which is required Unity to Run. Therefore to run tests in prallel we need to run second/third Unity instance.
Unity allow to open only one Unity per project. To run two Unity instances we need to duplicate project

Here are the main difficulty and trade off to consider
- If we duplicate only Assets and Packages directories then first Unity opening would be very long to generate Library/ (for some projects it's 1 hour+ which may kill all the profit of this)
- If we duplicate the whole project includes Library then we may stuck with long copying process (on large project (30 gb) with many assets with SSD it took for me 25-30 minutes because of large volume and many small files at Library)
- Here are option to use SymLink for Assets and Packages and just copy Library folder to speed up Unity opening time. But here might be issues with run parallel playmode tests because it creates a temp scenes at Assets/ folder
- Investigate [com.unity.multiplayer.playmode](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/index.html) package to see if it can help us with it

### Not support test case filters
Test case filters is not supported by Unity Test Runner the same way as it's for `dotnet test --filter`. Unity has less options and more traditional way to setup
[Link to documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestTools.TestRunner.Api.Filter.html#methods)

## Documentation links
- [Unity Test Runner](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/index.html)
- [Code Coverage](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/index.html)
- [Unity Path](https://docs.unity3d.com/Manual/EditorCommandLineArguments.html)
- [Unity CLI commands](https://docs.unity3d.com/Manual/EditorCommandLineArguments.html)
- [com.unity.multiplayer.playmode](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/index.html)
