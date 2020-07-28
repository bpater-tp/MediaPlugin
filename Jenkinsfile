node('macbot') {
    withEnv(['PATH=/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/share/dotnet:/Library/Frameworks/Mono.framework/Versions/Current/Commands:~/.dotnet/tools']) {
        timestamps {

            stage("Checkout") {
                checkout(
                    [
                        $class: 'GitSCM', 
                        branches: [[name: 'multinet']], 
                        doGenerateSubmoduleConfigurations: false, 
                        userRemoteConfigs: [[url: 'https://github.com/bpater-tp/MediaPlugin.git/']]
                    ]
                )
                sh "git clean -fdx"
            }

            stage("Build") {
                sh "./build.sh"
            }

            stage("Publish") {
                archiveArtifacts artifacts: "Build/nuget/**.nupkg", fingerprint: true
                withCredentials([string(credentialsId: 'nexus-nugettoken', variable: 'NUGETTOKEN')]) {
                    sh "nuget push 'Build/nuget/*.nupkg' -ApiKey $NUGETTOKEN -Source https://nexus.berlin.thinkproject.com/repository/nuget-tp/"
                }
            }
        }
    }
}
