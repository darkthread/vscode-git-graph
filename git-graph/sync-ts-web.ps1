
npm run compile-web
Copy-Item -Path ../media/* -Destination ./media -Recurse -Force
Copy-Item -Path ../web/*.html -Destination ./media -Force
