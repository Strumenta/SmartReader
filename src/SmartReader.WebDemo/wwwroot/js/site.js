$(document).ready(function () {
    $("#inputForm").submit(function () {
        analyze();
        return false;
    });
});

function analyze() {
    $.get("/home/analyze", { url: $("#inputUrl").val() })
        .done(function (data) {
            $("#articleContentHtml").empty();
            $("#articleContentHtml").html(data.content);
            $("#articleContentText").empty();
            $("#articleContentText").html(data.article.textContent.replace(/\n/g, "<br>"));
            $("#readerable").text(data.article.isReadable);
            $("#title").text(data.article.title);
            $("#dir").text(data.article.dir);
            $("#image").text(data.article.featuredImage);
            $("#byline").text(data.article.byline);
            $("#author").text(data.article.author);
            $("#publicationDate").text(data.article.publicationDate);
            $("#language").text(data.article.language);
            $("#length").text(data.article.length);
            $("#excerpt").text(data.article.excerpt);
            $("#timeToRead").text(data.article.timeToRead);
            $("#images").text(data.images);
        });
}