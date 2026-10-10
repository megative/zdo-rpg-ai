using ZdoRpgAi.Server.Game.Director;

namespace ZdoRpgAi.Tests;

public class SpeechChunksTests {
    [Theory]
    [InlineData("*Теруис откладывает шитьё.* Подвала нет? Это кто сказал?", "Подвала нет? Это кто сказал?")]
    [InlineData("Готово? *Она качает головой.* Так быстро?", "Готово? Так быстро?")]
    [InlineData("*Теруис поднимает взгляд.*\nЗал?", "Зал?")]
    [InlineData("Без ремарок.", "Без ремарок.")]
    public void DropsStageDirections(string text, string spoken) {
        Assert.Equal(spoken, SpeechChunks.WithoutStageDirections(text));
    }

    [Fact]
    public void NothingLeftIsNull() {
        Assert.Null(SpeechChunks.WithoutStageDirections("*молчит*"));
    }
}
