using iTunesSearch.Library;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Net.NetworkInformation;

namespace ItunesSearchConsole
{
    class Program
    {
        // Перечисление состояний
        enum STATES : int
        {
            FIRST_RUN = 0,
            CHECK_CONNECTION,
            USER_INPUT,
            EXIT
        }
        // Состояние программы.
        static int STATE;
        // Статус "сервера"
        static bool ONLINE;
        // База данных
        static SQLiteConnection sqlite_db;
        static void Main(string[] args)
        {
            STATE = (int)STATES.FIRST_RUN;
            ONLINE = false;
            MainLoop();
        }
        /// <summary>
        /// В зависимости от "состояния" переходим к необходимому пункту.
        /// При первом запуске проверяем базу данных, далее проверяем подключение и ожидаем ввода от пользователя.
        /// Пользователь может написать q или exit для выхода из программы.
        /// </summary>
        static void MainLoop()
        {
            while (STATE!=(int)STATES.EXIT)
            {
                switch (STATE)
                {
                    case (int)STATES.FIRST_RUN:
                        DBConnect();
                        STATE = (int)STATES.CHECK_CONNECTION;
                        break;
                    case (int)STATES.CHECK_CONNECTION:
                        ONLINE = PingHost("api.music.apple.com");
                        STATE = (int)STATES.USER_INPUT;
                        break;
                    case (int)STATES.USER_INPUT:
                        Console.WriteLine("Для выхода из программы можно ввести:q или exit");
                        Console.WriteLine("Введите имя исполнителя:");
                        String user_input = Console.ReadLine();
                        if(user_input=="q" || user_input=="exit")
                        {
                            STATE = (int)STATES.EXIT;
                            continue;
                        }
                        GetAlbum(user_input);
                        STATE = (int)STATES.CHECK_CONNECTION;
                        break;
                }
            }
        }
        /// <summary>
        /// Выводит данные об альбомах в консоль. В зависимости от "режима" онлайн или оффлайн достёт данные
        /// либо из sqlite, либо через запрос ItunesSearchManager
        /// </summary>
        /// <param name="user_input">Название альбома</param>
        static void GetAlbum(string user_input)
        {
            List<iTunesSearch.Library.Models.Album> cached;
            if (ONLINE == true)
            {
                iTunesSearchManager search = new iTunesSearchManager();
                var AsyncAlbums = search.GetAlbumsAsync(user_input);
                cached = AsyncAlbums.Result.Albums;
            }
            else
            {
                cached = ReadFromDB();
            }
            int i = 0;
            foreach (iTunesSearch.Library.Models.Album cached_album in cached)
            {
                if (cached_album.CollectionName.ToLower().Contains(user_input) || cached_album.ArtistName.ToLower().Contains(user_input))
                {
                    Console.WriteLine("===={0}====\nИсполниель:{1}\nДата выхода:{2};\nАльбом:{3}\n===========",
                        i++,
                        cached_album.ArtistName,
                        cached_album.ReleaseDate,
                        cached_album.CollectionName);
                    if (ONLINE == true) { WriteToDB(cached_album); }
                }
            }
        }
        /// <summary>
        /// Проверка "пинга"
        /// </summary>
        /// <param name="nameOrAddress"></param>
        /// <returns></returns>
        public static bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }
            if (pingable == true)
            {
                Console.WriteLine("Успешно подключено к {0}",nameOrAddress);
            }
            else
            {
                Console.WriteLine("Подключение не удалось. В качестве результата будут использованны кешированные данные.");
            }
            return pingable;
        }
        /// <summary>
        /// Подключение и проверка есть ли необходимая таблица в файле.
        /// </summary>
        static void DBConnect()
        {
            sqlite_db = new SQLiteConnection("Data Source=itunes_cache.db");
            try
            {
                sqlite_db.Open();
                // В случае, если нет необходимой таблицы мы должны её сделать.
                SQLiteCommand sqlite_cmd            = sqlite_db.CreateCommand();
                sqlite_cmd.CommandText              = @"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='search_cache';";
                SQLiteDataReader sqlite_datareader  = sqlite_cmd.ExecuteReader();
                sqlite_datareader.Read();
                if(sqlite_datareader.GetInt32(0)==0)
                {
                    sqlite_datareader.Close();
                    sqlite_cmd.CommandText = @"CREATE TABLE ""search_cache"" (
                                        ""key""   INTEGER NOT NULL UNIQUE,
                                        ""AMGArstisdID""  INTEGER,
	                                    ""ArtistID""  INTEGER,
	                                    ""ArtistName""    TEXT,
	                                    ""ArtistViewURL"" TEXT,
	                                    ""ArtworkURL100"" TEXT,
	                                    ""ArtworkURL60""  TEXT,
	                                    ""CollectionCensoredName""    TEXT,
	                                    ""CollectionExplicitness""    TEXT,
	                                    ""CollectionID""  INTEGER,
	                                    ""CollectionName""    TEXT,
	                                    ""CollectionPrice""   TEXT,
	                                    ""CollectionViewURL"" TEXT,
	                                    ""Copyright"" TEXT,
	                                    ""Country""   TEXT,
	                                    ""Currency""  TEXT,
	                                    ""PrimaryGenreName""  TEXT,
	                                    ""ReleaseDate""   TEXT,
	                                    ""TrackCount""    INTEGER,
	                                    PRIMARY KEY(""key"" AUTOINCREMENT)); ";
                    sqlite_cmd.ExecuteNonQuery();
                }
                else { sqlite_datareader.Close(); }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        /// <summary>
        /// Запись данных в таблицу search_cache, из файла itunes_cache
        /// </summary>
        /// <param name="temp_album"></param>
        static void WriteToDB(iTunesSearch.Library.Models.Album temp_album)
        {
            SQLiteCommand sqlite_cmd;
            sqlite_cmd                          = sqlite_db.CreateCommand();
            temp_album.CollectionName           = temp_album.CollectionName.Replace("\"","'");
            temp_album.CollectionCensoredName   = temp_album.CollectionCensoredName.Replace("\"", "'");
            temp_album.ArtistName               = temp_album.ArtistName.Replace("\"", "'");
            sqlite_cmd.CommandText = String.Format(@"INSERT INTO search_cache(AMGArstisdID,ArtistID,ArtistName,ArtistViewURL,ArtworkURL100,ArtworkURL60,CollectionCensoredName,CollectionExplicitness,CollectionID,CollectionName,CollectionPrice,CollectionViewURL,Copyright,Country,Currency,PrimaryGenreName,ReleaseDate,TrackCount)
                                    VALUES(""{0}"", ""{1}"", ""{2}"", ""{3}"", ""{4}"", ""{5}"", ""{6}"", ""{7}"", ""{8}"", ""{9}"", ""{10}"", ""{11}"", ""{12}"", ""{13}"", ""{14}"", ""{15}"", ""{16}"", ""{17}"")",
                                    temp_album.AMGArtistId,
                                    temp_album.ArtistId,
                                    temp_album.ArtistName,
                                    temp_album.ArtistViewUrl,
                                    temp_album.ArtworkUrl100,
                                    temp_album.ArtworkUrl60,
                                    temp_album.CollectionCensoredName,
                                    temp_album.CollectionExplicitness,
                                    temp_album.CollectionId,
                                    temp_album.CollectionName,
                                    temp_album.CollectionPrice,
                                    temp_album.CollectionViewUrl,
                                    temp_album.Copyright,
                                    temp_album.Country,
                                    temp_album.Currency,
                                    temp_album.PrimaryGenreName,
                                    temp_album.ReleaseDate,
                                    temp_album.TrackCount);
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Чтение всех данных из файла itunes_cache.db в таблице search_cache
        /// </summary>
        /// <returns>Возвращает list альбомов iTunesSearch.Library.Models.Album</returns>
        static List<iTunesSearch.Library.Models.Album> ReadFromDB()
        {
            SQLiteDataReader sqlite_datareader;
            SQLiteCommand sqlite_cmd;
            sqlite_cmd                                              = sqlite_db.CreateCommand();
            sqlite_cmd.CommandText                                  = "SELECT * FROM search_cache";
            List<iTunesSearch.Library.Models.Album> cached_albums   = new List<iTunesSearch.Library.Models.Album>();
            sqlite_datareader = sqlite_cmd.ExecuteReader();
            
            while (sqlite_datareader.Read())
            {
                try 
                {
                    iTunesSearch.Library.Models.Album temp_album = new iTunesSearch.Library.Models.Album();
                    temp_album.AMGArtistId              = sqlite_datareader.GetInt32(1);
                    temp_album.ArtistId                 = sqlite_datareader.GetInt32(2);
                    temp_album.ArtistName               = sqlite_datareader.GetString(3);
                    temp_album.ArtistViewUrl            = sqlite_datareader.GetString(4);
                    temp_album.ArtworkUrl100            = sqlite_datareader.GetString(5);
                    temp_album.ArtworkUrl60             = sqlite_datareader.GetString(6);
                    temp_album.CollectionCensoredName   = sqlite_datareader.GetString(7);
                    temp_album.CollectionExplicitness   = sqlite_datareader.GetString(8);
                    temp_album.CollectionId             = sqlite_datareader.GetInt32(9);
                    temp_album.CollectionName           = sqlite_datareader.GetString(10);
                    temp_album.CollectionPrice          = double.Parse(sqlite_datareader.GetString(11));
                    temp_album.CollectionViewUrl        = sqlite_datareader.GetString(12);
                    temp_album.Copyright                = sqlite_datareader.GetString(13);
                    temp_album.Country                  = sqlite_datareader.GetString(14);
                    temp_album.Currency                 = sqlite_datareader.GetString(15);
                    temp_album.PrimaryGenreName         = sqlite_datareader.GetString(16);
                    temp_album.ReleaseDate              = sqlite_datareader.GetString(17);
                    temp_album.TrackCount               = sqlite_datareader.GetInt32(18);
                    cached_albums.Add(temp_album);
                }
                catch (System.NullReferenceException ex)
                {
                    Console.WriteLine(ex);
                }
            }
            return cached_albums;
        }
    }
}