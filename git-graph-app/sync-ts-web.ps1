
npm run compile-web
Copy-Item -Path ../media/* -Destination ./media -Recurse -Force
Copy-Item -Path ../web/*.html -Destination ./media -Force
$rawIndexHtml = Get-Content ./media/index.html -Raw -Encoding utf8
$rawIndexHtml = $rawIndexHtml -replace '</body>', '<script src="/sse.js"></script></body>'
Set-Content ./media/index.html -Value $rawIndexHtml -Encoding utf8
